#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#import <os/log.h>

// Logs which UIKit callback iOS used to hand the app a link, so a Safari tap can be read without
// guessing. A universal link arrives as an NSUserActivity (scene:continueUserActivity:, or
// scene:willConnectToSession:options: on a cold start); a custom-scheme open arrives as a
// UIOpenURLContext. Unity folds both into one open-URL notification and Teak's own logs don't name
// the path, so this wraps the UnityScene / UnityAppController methods directly. Each wrapper calls
// the original, so Unity and Teak's hooks behave as before.
//
//   [Test Driver] link delivery path=<callback> type=<activity type | url-context> url=<url>
//
// It also repeats two Teak events the same way, since the device log hides Teak's own lines and
// Unity only sees native events after its scripts have started:
//
//   [Test Driver] teak event request.send users.json launch_link=<url | (none)>
//   [Test Driver] teak event deep_link.<name> <message or event data as JSON>
//
// %{public} keeps the device log from replacing the values with <private>.

static void LogDelivery(NSString* path, NSString* type, NSURL* url) {
    os_log(OS_LOG_DEFAULT, "[Test Driver] link delivery path=%{public}@ type=%{public}@ url=%{public}@", path, type, url.absoluteString);
}

// event_data as one line: a string as is, a dictionary as JSON.
static NSString* OneLine(id data) {
    if (![NSJSONSerialization isValidJSONObject:data]) return [data description];
    NSData* json = [NSJSONSerialization dataWithJSONObject:data options:0 error:nil];
    return json ? [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding] : [data description];
}

static void LogTeakEvent(NSString* event, NSDictionary* payload) {
    id data = payload[@"event_data"];
    if ([event isEqualToString:@"request.send"] && [data isKindOfClass:[NSDictionary class]]) {
        if ([data[@"endpoint"] hasSuffix:@"/users.json"] && [data[@"payload"] isKindOfClass:[NSDictionary class]]) {
            id launchLink = data[@"payload"][@"launch_link"];
            os_log(OS_LOG_DEFAULT, "[Test Driver] teak event request.send users.json launch_link=%{public}@", launchLink ?: @"(none)");
        }
    } else if ([event hasPrefix:@"deep_link."]) {
        id message = [data isKindOfClass:[NSDictionary class]] && data[@"message"] != nil ? data[@"message"] : data;
        os_log(OS_LOG_DEFAULT, "[Test Driver] teak event %{public}@ %{public}@", event, OneLine(message));
    }
}

// Puts LogTeakEvent in front of Teak's log listener, which Unity's bridge installs. Done from the
// first link callback, by which time that bridge has run; the listener is called from any thread.
static void WrapTeakLogListener(void) {
    static BOOL wrapped = NO;
    if (wrapped) return;
    Class teakClass = NSClassFromString(@"Teak");
    id teak = [teakClass respondsToSelector:@selector(sharedInstance)] ? [teakClass performSelector:@selector(sharedInstance)] : nil;
    if (teak == nil) return;
    void (^original)(NSString*, NSString*, NSDictionary*) = [teak valueForKey:@"logListener"];
    [teak setValue:^(NSString* event, NSString* level, NSDictionary* payload) {
        LogTeakEvent(event, payload);
        if (original != nil) original(event, level, payload);
    } forKey:@"logListener"];
    wrapped = YES;
    os_log(OS_LOG_DEFAULT, "[Test Driver] teak log listener wrapped, had one: %{public}@", original != nil ? @"yes" : @"no");
}

static void LogActivity(NSString* path, NSUserActivity* activity) {
    LogDelivery(path, activity.activityType, activity.webpageURL);
}

static void LogContext(NSString* path, UIOpenURLContext* context) API_AVAILABLE(ios(13.0));
static void LogContext(NSString* path, UIOpenURLContext* context) {
    LogDelivery(path, @"url-context", context.URL);
}

// Swaps in hook for klass's sel and stores the IMP it replaced. NO if klass doesn't implement sel.
static BOOL Hook(Class klass, SEL sel, IMP hook, IMP* original) {
    Method method = klass ? class_getInstanceMethod(klass, sel) : NULL;
    if (method == NULL) return NO;
    *original = method_setImplementation(method, hook);
    return YES;
}

static void (*sSceneConnect)(id, SEL, UIScene*, UISceneSession*, UISceneConnectionOptions*);
static void (*sSceneActivity)(id, SEL, UIScene*, NSUserActivity*);
static void (*sSceneOpenContexts)(id, SEL, UIScene*, NSSet<UIOpenURLContext*>*);
static BOOL (*sAppActivity)(id, SEL, UIApplication*, NSUserActivity*, id);
static BOOL (*sAppOpenURL)(id, SEL, UIApplication*, NSURL*, NSDictionary*);

API_AVAILABLE(ios(13.0))
static void SceneConnect(id self, SEL cmd, UIScene* scene, UISceneSession* session, UISceneConnectionOptions* options) {
    WrapTeakLogListener();
    for (NSUserActivity* activity in options.userActivities) LogActivity(@"scene:willConnectToSession:options:", activity);
    for (UIOpenURLContext* context in options.URLContexts) LogContext(@"scene:willConnectToSession:options:", context);
    sSceneConnect(self, cmd, scene, session, options);
}

static void SceneActivity(id self, SEL cmd, UIScene* scene, NSUserActivity* activity) {
    WrapTeakLogListener();
    LogActivity(@"scene:continueUserActivity:", activity);
    sSceneActivity(self, cmd, scene, activity);
}

API_AVAILABLE(ios(13.0))
static void SceneOpenContexts(id self, SEL cmd, UIScene* scene, NSSet<UIOpenURLContext*>* contexts) {
    for (UIOpenURLContext* context in contexts) LogContext(@"scene:openURLContexts:", context);
    sSceneOpenContexts(self, cmd, scene, contexts);
}

static BOOL AppActivity(id self, SEL cmd, UIApplication* application, NSUserActivity* activity, id handler) {
    WrapTeakLogListener();
    LogActivity(@"application:continueUserActivity:restorationHandler:", activity);
    return sAppActivity(self, cmd, application, activity, handler);
}

static BOOL AppOpenURL(id self, SEL cmd, UIApplication* application, NSURL* url, NSDictionary* options) {
    LogDelivery(@"application:openURL:options:", @"url", url);
    return sAppOpenURL(self, cmd, application, url, options);
}

API_AVAILABLE(ios(13.0))
static BOOL HookScene(Class klass) {
    BOOL hooked = NO;
    hooked |= Hook(klass, @selector(scene:willConnectToSession:options:), (IMP)SceneConnect, (IMP*)&sSceneConnect);
    hooked |= Hook(klass, @selector(scene:continueUserActivity:), (IMP)SceneActivity, (IMP*)&sSceneActivity);
    hooked |= Hook(klass, @selector(scene:openURLContexts:), (IMP)SceneOpenContexts, (IMP*)&sSceneOpenContexts);
    return hooked;
}

// The legacy (app delegate) lifecycle.
static BOOL HookAppDelegate(Class klass) {
    BOOL hooked = NO;
    hooked |= Hook(klass, @selector(application:continueUserActivity:restorationHandler:), (IMP)AppActivity, (IMP*)&sAppActivity);
    hooked |= Hook(klass, @selector(application:openURL:options:), (IMP)AppOpenURL, (IMP*)&sAppOpenURL);
    return hooked;
}

@interface TeakCleanroomLinkDeliveryLog : NSObject
@end

@implementation TeakCleanroomLinkDeliveryLog

+ (void)load {
    BOOL scene = NO;
    if (@available(iOS 13.0, *)) {
        scene = HookScene(NSClassFromString(@"UnityScene"));
    }
    BOOL appDelegate = HookAppDelegate(NSClassFromString(@"UnityAppController"));
    // Says which hooks took, so a missing delivery line can be told apart from a hook that never installed.
    NSLog(@"[Test Driver] link delivery hooks: UnityScene=%@ UnityAppController=%@", scene ? @"yes" : @"no", appDelegate ? @"yes" : @"no");
}

@end

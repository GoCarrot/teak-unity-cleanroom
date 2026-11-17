#import <UIKit/UIKit.h>
#import <objc/runtime.h>

// Logs which UIKit callback iOS used to hand the app a link, so a Safari tap can be read without
// guessing. A universal link arrives as an NSUserActivity (scene:continueUserActivity:, or
// scene:willConnectToSession:options: on a cold start); a custom-scheme open arrives as a
// UIOpenURLContext. Unity folds both into one open-URL notification and Teak's own logs don't name
// the path, so this wraps the UnityScene / UnityAppController methods directly. Each wrapper calls
// the original, so Unity and Teak's hooks behave as before.
//
//   [Test Driver] link delivery path=<callback> type=<activity type | url-context> url=<url>

static void LogDelivery(NSString* path, NSString* type, NSURL* url) {
    NSLog(@"[Test Driver] link delivery path=%@ type=%@ url=%@", path, type, url.absoluteString);
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
    for (NSUserActivity* activity in options.userActivities) LogActivity(@"scene:willConnectToSession:options:", activity);
    for (UIOpenURLContext* context in options.URLContexts) LogContext(@"scene:willConnectToSession:options:", context);
    sSceneConnect(self, cmd, scene, session, options);
}

static void SceneActivity(id self, SEL cmd, UIScene* scene, NSUserActivity* activity) {
    LogActivity(@"scene:continueUserActivity:", activity);
    sSceneActivity(self, cmd, scene, activity);
}

API_AVAILABLE(ios(13.0))
static void SceneOpenContexts(id self, SEL cmd, UIScene* scene, NSSet<UIOpenURLContext*>* contexts) {
    for (UIOpenURLContext* context in contexts) LogContext(@"scene:openURLContexts:", context);
    sSceneOpenContexts(self, cmd, scene, contexts);
}

static BOOL AppActivity(id self, SEL cmd, UIApplication* application, NSUserActivity* activity, id handler) {
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

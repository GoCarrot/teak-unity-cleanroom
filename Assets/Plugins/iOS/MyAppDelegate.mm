#import <Foundation/Foundation.h>
#import "AppDelegateListener.h"
#import "UnityInterface.h"

// Non-Teak deep link test hook. Under the Unity 6 UIScene lifecycle iOS routes
// URL opens to scene:openURLContexts: rather than the app delegate, so a
// UnityAppController subclass overriding application:openURL: is never called.
// We instead observe kUnityOnOpenURL via Unity's AppDelegateListener, which is
// posted on both the legacy app-delegate and the UnityScene paths -- one
// lifecycle-agnostic hook. (See C-852.)
@interface NonTeakDeepLinkListener : NSObject <AppDelegateListener>
@end

@implementation NonTeakDeepLinkListener

+ (void)load {
    // NSNotificationCenter does not retain observers, so hold the listener for
    // the lifetime of the app.
    static NonTeakDeepLinkListener* listener = nil;
    listener = [[NonTeakDeepLinkListener alloc] init];
    UnityRegisterAppDelegateListener(listener);
}

- (void)onOpenURL:(NSNotification*)notification {
    id urlValue = notification.userInfo[@"url"];
    NSURL* url = nil;
    if ([urlValue isKindOfClass:[NSURL class]]) {
        url = (NSURL*)urlValue;
    } else if ([urlValue isKindOfClass:[NSString class]]) {
        url = [NSURL URLWithString:(NSString*)urlValue];
    }
    [self handleOpenURL:url];
}

- (void)handleOpenURL:(NSURL*)url {
    if (![url.scheme isEqualToString:@"nonteak"]) {
        return;
    }

    NSDictionary* jsonDict = @{
        @"run_id" : @0,
        @"event_id" : @0,
        @"timestamp" : @0,
        @"event_type" : @"test.delegate",
        @"log_level" : @"INFO",
        @"event_data" : @{
            @"url" : url.absoluteString,
            @"method" : @"onOpenURL:"
        }
    };

    NSError* error = nil;
    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:jsonDict options:0 error:&error];

    if (error == nil) {
        NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        UnitySendMessage("TeakGameObject", "LogEvent", [jsonString UTF8String]);
    }
}

@end

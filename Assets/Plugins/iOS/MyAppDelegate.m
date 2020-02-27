#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#import "UnityAppController.h"

@interface MyAppDelegate : UnityAppController
@end

IMPL_APP_CONTROLLER_SUBCLASS(MyAppDelegate)

@implementation MyAppDelegate

- (BOOL)handleOpenURL:(NSURL*)url fromSelector:(SEL)sel {
    if (![url.scheme isEqualToString:@"nonteak"]) {
        return NO;
    }

    NSDictionary* jsonDict = @{
        @"run_id" : @0,
        @"event_id" : @0,
        @"timestamp" : @0,
        @"event_type" : @"test.delegate",
        @"log_level" : @"INFO",
        @"event_data" : @{
            @"url" : url.absoluteString,
            @"method" : NSStringFromSelector(sel)
        }
    };

    NSError* error = nil;
    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:jsonDict options:0 error:&error];

    if (error == nil) {
        NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        UnitySendMessage("TeakGameObject", "LogEvent", [jsonString UTF8String]);
    }

    return YES;
}

- (BOOL)application:(UIApplication*)application openURL:(NSURL*)url sourceApplication:(NSString*)sourceApplication annotation:(id)annotation {
    [super application:application openURL:url sourceApplication:sourceApplication annotation:annotation];
    return [self handleOpenURL:url fromSelector:_cmd];
}

- (BOOL)application:(UIApplication*)application openURL:(NSURL*)url options:(NSDictionary<NSString*, id>*)options {
    if (class_respondsToSelector(UnityAppController.class, _cmd)) {
        [super application:application openURL:url options:options];
        return [self handleOpenURL:url fromSelector:_cmd];
    }

    return [self application:application openURL:url sourceApplication:options[UIApplicationOpenURLOptionsSourceApplicationKey] annotation:options[UIApplicationOpenURLOptionsAnnotationKey]];
}

@end

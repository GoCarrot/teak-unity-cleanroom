#import <UIKit/UIKit.h>

extern void TeakCleanroomStartUpdatesProbe(void);

// Starts the Transaction.updates probe from native code at didFinishLaunching, before any Unity
// script runs, and logs the app and scene lifecycle notifications with timestamps.
@interface TeakCleanroomUpdatesLoader : NSObject
@end

@implementation TeakCleanroomUpdatesLoader

+ (void)load {
  NSArray<NSNotificationName>* names = @[
    UIApplicationDidFinishLaunchingNotification, UIApplicationWillResignActiveNotification,
    UIApplicationDidBecomeActiveNotification, UIApplicationDidEnterBackgroundNotification,
    UIApplicationWillEnterForegroundNotification, UISceneWillDeactivateNotification,
    UISceneDidActivateNotification, UISceneDidEnterBackgroundNotification,
    UISceneWillEnterForegroundNotification
  ];
  for (NSNotificationName name in names) {
    [[NSNotificationCenter defaultCenter] addObserverForName:name
                                                      object:nil
                                                       queue:nil
                                                  usingBlock:^(NSNotification* note) {
                                                    NSLog(@"[UpdatesProbe] lifecycle %@", note.name);
                                                    if ([note.name isEqualToString:UIApplicationDidFinishLaunchingNotification]) {
                                                      TeakCleanroomStartUpdatesProbe();
                                                    }
                                                  }];
  }
}

@end

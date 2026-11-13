#import <UIKit/UIKit.h>
#import "SK2Observer.h"

// C-2320 device probe: installs the SK2Observer GOT hooks (Transaction.finish() and
// VerificationResult<Transaction>.jwsRepresentation) at didFinishLaunching, when UnityFramework
// and its IAP 5 Swift code are bound. Hook output is NSLog'd with a "PROBE [objc]" prefix.
@interface TeakCleanroomSK2ObserverLoader : NSObject
@end

@implementation TeakCleanroomSK2ObserverLoader

+ (void)load {
  [[NSNotificationCenter defaultCenter] addObserverForName:UIApplicationDidFinishLaunchingNotification
                                                    object:nil
                                                     queue:nil
                                                usingBlock:^(NSNotification* note) {
                                                  NSInteger finish = [SK2Observer installFinishHook];
                                                  NSInteger jws = [SK2Observer installJWSHook];
                                                  NSLog(@"PROBE [objc] loader: finish slots=%ld jws slots=%ld", (long)finish, (long)jws);
                                                }];
}

@end

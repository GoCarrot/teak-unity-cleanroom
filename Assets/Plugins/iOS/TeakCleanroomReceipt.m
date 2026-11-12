#import <Foundation/Foundation.h>

// Path of the app receipt, as StoreKit reports it for this build (sandboxReceipt under a
// sandbox or development signature, receipt otherwise).
const char* TeakCleanroomReceiptPath(void) {
  NSString* path = [[[NSBundle mainBundle] appStoreReceiptURL] path];
  return strdup(path == nil ? "" : path.UTF8String);
}

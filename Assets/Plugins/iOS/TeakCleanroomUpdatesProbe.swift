import Foundation
import StoreKit

// Spike probe: listens to Transaction.updates from launch and logs everything it emits, with
// NSLog timestamps that line up with Teak's lifecycle lines and the purchase callbacks.
// Read-only: it never finishes a transaction.
@available(iOS 15.0, *)
enum TeakCleanroomUpdatesProbe {
    static var task: Task<Void, Never>?

    static func start() {
        guard task == nil else { return }
        NSLog("[UpdatesProbe] starting Transaction.updates listener")
        task = Task.detached {
            NSLog("[UpdatesProbe] updates listener live (awaiting Transaction.updates)")
            for await result in Transaction.updates {
                switch result {
                case .verified(let t):
                    NSLog("[UpdatesProbe] updates emitted verified id=%llu product=%@ originalID=%llu purchaseDate=%@",
                          t.id, t.productID, t.originalID, "\(t.purchaseDate)")
                case .unverified(let t, let error):
                    NSLog("[UpdatesProbe] updates emitted UNVERIFIED id=%llu product=%@ error=%@", t.id, t.productID, "\(error)")
                }
            }
            NSLog("[UpdatesProbe] updates sequence ended")
        }
    }
}

@_cdecl("TeakCleanroomStartUpdatesProbe")
public func TeakCleanroomStartUpdatesProbe() {
    if #available(iOS 15.0, *) {
        TeakCleanroomUpdatesProbe.start()
    } else {
        NSLog("[UpdatesProbe] iOS < 15, no Transaction.updates")
    }
}

// A native log line with a timestamp, so Unity-side callbacks can be ordered against lifecycle lines.
@_cdecl("TeakCleanroomNSLog")
public func TeakCleanroomNSLog(_ message: UnsafePointer<CChar>) {
    NSLog("[UpdatesProbe] %@", String(cString: message))
}

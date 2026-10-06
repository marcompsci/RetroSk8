// Retro Sk8 - verified purchases (Phase 19). Asks StoreKit 2 which of this app's products the current Apple ID
// owns. StoreKit checks Apple's signature on every transaction (JWS) before reporting it as .verified, so a faked
// purchase on a modified device doesn't show up here. Called from StoreService.cs via DllImport("__Internal").
//
// One request at a time: call RetroSk8_EntitlementsRefresh, poll RetroSk8_EntitlementsState
// (0 idle, 1 busy, 2 done, 3 failed) and read RetroSk8_EntitlementsResult (comma-separated product ids when done).
import Foundation
import StoreKit

private let rs8EntitlementLock = NSLock()
private var rs8EntitlementState: Int32 = 0
private var rs8EntitlementResult = ""

private func rs8SetEntitlements(_ state: Int32, _ text: String) {
    rs8EntitlementLock.lock()
    rs8EntitlementState = state
    rs8EntitlementResult = text
    rs8EntitlementLock.unlock()
}

@_cdecl("RetroSk8_EntitlementsRefresh")
public func RetroSk8_EntitlementsRefresh() {
    rs8SetEntitlements(1, "")
    guard #available(iOS 15.0, *) else {
        rs8SetEntitlements(3, "STOREKIT 2 NEEDS IOS 15")
        return
    }
    Task.detached {
        var ids: [String] = []
        for await result in Transaction.currentEntitlements {
            // Only transactions whose Apple signature checks out, and that weren't refunded.
            if case .verified(let transaction) = result, transaction.revocationDate == nil {
                ids.append(transaction.productID)
            }
        }
        rs8SetEntitlements(2, ids.joined(separator: ","))
    }
}

@_cdecl("RetroSk8_EntitlementsState")
public func RetroSk8_EntitlementsState() -> Int32 {
    rs8EntitlementLock.lock()
    defer { rs8EntitlementLock.unlock() }
    return rs8EntitlementState
}

// The caller (IL2CPP marshalling) frees the returned copy.
@_cdecl("RetroSk8_EntitlementsResult")
public func RetroSk8_EntitlementsResult() -> UnsafeMutablePointer<CChar>? {
    rs8EntitlementLock.lock()
    defer { rs8EntitlementLock.unlock() }
    return strdup(rs8EntitlementResult)
}

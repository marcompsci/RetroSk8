// Retro Sk8 - App Store bridge for the optional cosmetic packs (StoreKit, non-consumable products).
// Called from StoreService.cs via DllImport("__Internal"). Events are queued here and polled from C# once per frame
// as "kind|productId|message" lines. A purchase is only finished (removed from the App Store queue) after C# has
// unlocked it and called RetroSk8_StoreFinish, so a crash between paying and unlocking can't lose it: StoreKit
// redelivers unfinished transactions on the next launch.
#import <Foundation/Foundation.h>
#import <StoreKit/StoreKit.h>
#include <string.h>
#include <stdlib.h>

#if __has_feature(objc_arc)
#define RSKS_RELEASE(x)
#define RSKS_RETAIN(x) (x)
#else
#define RSKS_RELEASE(x) [(x) release]
#define RSKS_RETAIN(x) [(x) retain]
#endif

@interface RetroSk8StoreObserver : NSObject <SKPaymentTransactionObserver, SKProductsRequestDelegate>
@end

static RetroSk8StoreObserver *s_storeObserver = nil;
static NSMutableArray *s_storeEvents = nil;          // NSString lines
static NSMutableDictionary *s_storeProducts = nil;   // productId -> SKProduct
static NSMutableDictionary *s_storePending = nil;    // productId -> NSMutableArray of SKPaymentTransaction
static SKProductsRequest *s_storeRequest = nil;

static NSString *RetroSk8StoreClean(NSString *text)
{
    if (text == nil) return @"";
    return [[text stringByReplacingOccurrencesOfString:@"|" withString:@"/"] stringByReplacingOccurrencesOfString:@"\n" withString:@" "];
}

static void RetroSk8StorePush(NSString *kind, NSString *productId, NSString *message)
{
    NSString *line = [NSString stringWithFormat:@"%@|%@|%@", kind, RetroSk8StoreClean(productId), RetroSk8StoreClean(message)];
    @synchronized (s_storeEvents) { [s_storeEvents addObject:line]; }
}

static void RetroSk8StoreHold(SKPaymentTransaction *t)
{
    NSString *pid = t.payment.productIdentifier;
    @synchronized (s_storePending)
    {
        NSMutableArray *list = [s_storePending objectForKey:pid];
        if (list == nil)
        {
            list = [NSMutableArray array];
            [s_storePending setObject:list forKey:pid];
        }
        [list addObject:t];
    }
}

@implementation RetroSk8StoreObserver

- (void)productsRequest:(SKProductsRequest *)request didReceiveResponse:(SKProductsResponse *)response
{
    @synchronized (s_storeProducts)
    {
        for (SKProduct *p in response.products) [s_storeProducts setObject:p forKey:p.productIdentifier];
    }
    RetroSk8StorePush(@"products", @"", [NSString stringWithFormat:@"%lu", (unsigned long)response.products.count]);
}

- (void)request:(SKRequest *)request didFailWithError:(NSError *)error
{
    RetroSk8StorePush(@"products", @"", error.localizedDescription);
}

- (void)paymentQueue:(SKPaymentQueue *)queue updatedTransactions:(NSArray<SKPaymentTransaction *> *)transactions
{
    for (SKPaymentTransaction *t in transactions)
    {
        NSString *pid = t.payment.productIdentifier;
        switch (t.transactionState)
        {
            case SKPaymentTransactionStatePurchased:
                RetroSk8StoreHold(t);
                RetroSk8StorePush(@"purchased", pid, @"");
                break;
            case SKPaymentTransactionStateRestored:
                RetroSk8StoreHold(t);
                RetroSk8StorePush(@"restored", pid, @"");
                break;
            case SKPaymentTransactionStateFailed:
            {
                BOOL cancelled = t.error != nil && t.error.code == SKErrorPaymentCancelled;
                RetroSk8StorePush(cancelled ? @"cancelled" : @"failed", pid, t.error != nil ? t.error.localizedDescription : @"");
                [queue finishTransaction:t];
                break;
            }
            case SKPaymentTransactionStateDeferred:
                RetroSk8StorePush(@"deferred", pid, @"");
                break;
            default:
                break; // purchasing: wait
        }
    }
}

- (void)paymentQueueRestoreCompletedTransactionsFinished:(SKPaymentQueue *)queue
{
    RetroSk8StorePush(@"restoreDone", @"", @"");
}

- (void)paymentQueue:(SKPaymentQueue *)queue restoreCompletedTransactionsFailedWithError:(NSError *)error
{
    RetroSk8StorePush(@"restoreFailed", @"", error != nil ? error.localizedDescription : @"");
}

@end

// Starts listening to the App Store queue and asks for the products' local prices. csvIds: "id1,id2,...".
extern "C" void RetroSk8_StoreInit(const char *csvIds)
{
    if (s_storeObserver == nil)
    {
        s_storeEvents = [[NSMutableArray alloc] init];
        s_storeProducts = [[NSMutableDictionary alloc] init];
        s_storePending = [[NSMutableDictionary alloc] init];
        s_storeObserver = [[RetroSk8StoreObserver alloc] init];
        [[SKPaymentQueue defaultQueue] addTransactionObserver:s_storeObserver];
    }
    if (csvIds == NULL) return;
    NSArray *ids = [[NSString stringWithUTF8String:csvIds] componentsSeparatedByString:@","];
    if (s_storeRequest != nil)
    {
        s_storeRequest.delegate = nil;
        [s_storeRequest cancel];
        RSKS_RELEASE(s_storeRequest);
    }
    s_storeRequest = [[SKProductsRequest alloc] initWithProductIdentifiers:[NSSet setWithArray:ids]];
    s_storeRequest.delegate = s_storeObserver;
    [s_storeRequest start];
}

extern "C" int RetroSk8_StoreCanPay(void)
{
    return [SKPaymentQueue canMakePayments] ? 1 : 0;
}

// The product's price in the player's currency ("" until the App Store has answered). The caller frees the copy.
extern "C" char *RetroSk8_StorePrice(const char *productId)
{
    if (productId == NULL || s_storeProducts == nil) return strdup("");
    SKProduct *p = nil;
    @synchronized (s_storeProducts) { p = [s_storeProducts objectForKey:[NSString stringWithUTF8String:productId]]; }
    if (p == nil) return strdup("");
    NSNumberFormatter *f = [[NSNumberFormatter alloc] init];
    f.numberStyle = NSNumberFormatterCurrencyStyle;
    f.locale = p.priceLocale;
    NSString *text = [f stringFromNumber:p.price];
    RSKS_RELEASE(f);
    const char *utf8 = text != nil ? [text UTF8String] : "";
    return strdup(utf8 != NULL ? utf8 : "");
}

extern "C" void RetroSk8_StoreBuy(const char *productId)
{
    if (productId == NULL || s_storeObserver == nil) return;
    NSString *pid = [NSString stringWithUTF8String:productId];
    SKProduct *p = nil;
    @synchronized (s_storeProducts) { p = [s_storeProducts objectForKey:pid]; }
    if (p == nil) { RetroSk8StorePush(@"failed", pid, @"This pack isn't available from the App Store right now."); return; }
    if (![SKPaymentQueue canMakePayments]) { RetroSk8StorePush(@"failed", pid, @"Purchases are turned off on this device."); return; }
    [[SKPaymentQueue defaultQueue] addPayment:[SKPayment paymentWithProduct:p]];
}

extern "C" void RetroSk8_StoreRestore(void)
{
    if (s_storeObserver == nil) return;
    [[SKPaymentQueue defaultQueue] restoreCompletedTransactions];
}

// Next queued event, or "" when there are none. The caller frees the copy.
extern "C" char *RetroSk8_StorePoll(void)
{
    if (s_storeEvents == nil) return strdup("");
    NSString *line = nil;
    @synchronized (s_storeEvents)
    {
        if (s_storeEvents.count > 0)
        {
            line = RSKS_RETAIN([s_storeEvents objectAtIndex:0]);
            [s_storeEvents removeObjectAtIndex:0];
        }
    }
    if (line == nil) return strdup("");
    char *copy = strdup([line UTF8String]);
    RSKS_RELEASE(line);
    return copy;
}

// Called once C# has unlocked the product: finishes every held transaction for it.
extern "C" void RetroSk8_StoreFinish(const char *productId)
{
    if (productId == NULL || s_storePending == nil) return;
    NSString *pid = [NSString stringWithUTF8String:productId];
    NSArray *list = nil;
    @synchronized (s_storePending)
    {
        list = RSKS_RETAIN([s_storePending objectForKey:pid]);
        [s_storePending removeObjectForKey:pid];
    }
    for (SKPaymentTransaction *t in list) [[SKPaymentQueue defaultQueue] finishTransaction:t];
    RSKS_RELEASE(list);
}

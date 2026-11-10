/**************************************************************************/
/*  device_metrics.h                                                      */

#pragma once

#import <Foundation/Foundation.h>

@interface GDTDeviceMetrics : NSObject

@property(nonatomic, class, readonly, strong) NSDictionary<NSArray *, NSNumber *> *dpiList;
@property(nonatomic, class, readonly, strong) NSDictionary<NSArray *, NSNumber *> *dpiListInternal;

@end

#import <Cocoa/Cocoa.h>
#import <ApplicationServices/ApplicationServices.h>
#include <errno.h>
#include <fcntl.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/file.h>
#include <unistd.h>

static const CGFloat kWindowSize = 26.0;
static const CGFloat kCrosshairRadius = 8.5;
static const CGFloat kCenterGap = 2.0;

@interface CrosshairView : NSView
@end

@implementation CrosshairView

- (BOOL)isOpaque
{
    return NO;
}

- (void)drawRect:(NSRect)dirtyRect
{
    [super drawRect:dirtyRect];
    [[NSColor clearColor] setFill];
    NSRectFillUsingOperation(self.bounds, NSCompositingOperationClear);

    CGFloat center = kWindowSize / 2.0;
    NSBezierPath *path = [NSBezierPath bezierPath];
    [path moveToPoint:NSMakePoint(center - kCrosshairRadius, center)];
    [path lineToPoint:NSMakePoint(center - kCenterGap, center)];
    [path moveToPoint:NSMakePoint(center + kCenterGap, center)];
    [path lineToPoint:NSMakePoint(center + kCrosshairRadius, center)];
    [path moveToPoint:NSMakePoint(center, center - kCrosshairRadius)];
    [path lineToPoint:NSMakePoint(center, center - kCenterGap)];
    [path moveToPoint:NSMakePoint(center, center + kCenterGap)];
    [path lineToPoint:NSMakePoint(center, center + kCrosshairRadius)];
    [path setLineCapStyle:NSLineCapStyleRound];

    [[NSColor colorWithWhite:0.0 alpha:0.9] setStroke];
    [path setLineWidth:2.0];
    [path stroke];

    [[NSColor colorWithWhite:1.0 alpha:0.98] setStroke];
    [path setLineWidth:0.85];
    [path stroke];
}

@end

@interface CrosshairDelegate : NSObject <NSApplicationDelegate>
@property(nonatomic, strong) NSPanel *window;
@property(nonatomic, strong) NSTimer *trackingTimer;
@property(nonatomic, strong) NSTimer *ownerTimer;
@property(nonatomic, assign) pid_t ownerPid;
@end

@implementation CrosshairDelegate

- (void)applicationDidFinishLaunching:(NSNotification *)notification
{
    NSRect frame = NSMakeRect(0.0, 0.0, kWindowSize, kWindowSize);
    self.window = [[NSPanel alloc]
        initWithContentRect:frame
                  styleMask:NSWindowStyleMaskBorderless | NSWindowStyleMaskNonactivatingPanel
                    backing:NSBackingStoreBuffered
                      defer:NO];

    [self.window setOpaque:NO];
    [self.window setBackgroundColor:[NSColor clearColor]];
    [self.window setHasShadow:NO];
    [self.window setIgnoresMouseEvents:YES];
    [self.window setHidesOnDeactivate:NO];
    [self.window setFloatingPanel:YES];
    [self.window setLevel:CGWindowLevelForKey(kCGOverlayWindowLevelKey)];
    [self.window setCollectionBehavior:
        NSWindowCollectionBehaviorCanJoinAllSpaces |
        NSWindowCollectionBehaviorFullScreenAuxiliary |
        NSWindowCollectionBehaviorStationary |
        NSWindowCollectionBehaviorIgnoresCycle];
    [self.window setContentView:[[CrosshairView alloc] initWithFrame:frame]];

    [self updatePosition:nil];
    [self.window orderFrontRegardless];

    self.trackingTimer = [NSTimer timerWithTimeInterval:(1.0 / 120.0)
                                                 target:self
                                               selector:@selector(updatePosition:)
                                               userInfo:nil
                                                repeats:YES];
    [[NSRunLoop mainRunLoop] addTimer:self.trackingTimer forMode:NSRunLoopCommonModes];

    if (self.ownerPid > 1) {
        self.ownerTimer = [NSTimer timerWithTimeInterval:1.0
                                                  target:self
                                                selector:@selector(checkOwner:)
                                                userInfo:nil
                                                 repeats:YES];
        [[NSRunLoop mainRunLoop] addTimer:self.ownerTimer forMode:NSRunLoopCommonModes];
    }
}

- (void)updatePosition:(NSTimer *)timer
{
    NSPoint cursor = [NSEvent mouseLocation];
    [self.window setFrameOrigin:NSMakePoint(
        cursor.x - kWindowSize / 2.0,
        cursor.y - kWindowSize / 2.0)];
}

- (void)checkOwner:(NSTimer *)timer
{
    if (kill(self.ownerPid, 0) != 0 && errno == ESRCH)
        [NSApp terminate:nil];
}

@end

static int acquire_toggle_lock(void)
{
    char path[128];
    snprintf(path, sizeof(path), "/tmp/tablet-utility-crosshair-%u.lock", getuid());

    int fd = open(path, O_CREAT | O_RDWR, 0600);
    if (fd < 0)
        return -1;

    if (flock(fd, LOCK_EX | LOCK_NB) != 0) {
        char buffer[32] = {0};
        lseek(fd, 0, SEEK_SET);
        ssize_t length = read(fd, buffer, sizeof(buffer) - 1);
        if (length > 0) {
            pid_t runningPid = (pid_t)strtol(buffer, NULL, 10);
            if (runningPid > 1)
                kill(runningPid, SIGTERM);
        }
        close(fd);
        return -1;
    }

    ftruncate(fd, 0);
    dprintf(fd, "%d\n", getpid());
    fsync(fd);
    return fd;
}

int main(int argc, const char *argv[])
{
    @autoreleasepool {
        int lockFd = acquire_toggle_lock();
        if (lockFd < 0)
            return 0;

        const char *action = argc > 1 ? argv[1] : "toggle";
        if (strcmp(action, "off") == 0) {
            close(lockFd);
            return 0;
        }

        pid_t ownerPid = argc > 2 ? (pid_t)strtol(argv[2], NULL, 10) : 0;

        NSApplication *application = [NSApplication sharedApplication];
        [application setActivationPolicy:NSApplicationActivationPolicyAccessory];

        CrosshairDelegate *delegate = [[CrosshairDelegate alloc] init];
        delegate.ownerPid = ownerPid;
        [application setDelegate:delegate];
        [application run];

        close(lockFd);
    }

    return 0;
}

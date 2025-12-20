# Notification System Fixes

## Issues Identified

1. **Service Worker not registered in development** - The service worker was only registered in production mode, but notifications need it to work properly
2. **Missing error handling** - Notifications API calls were not wrapped in proper try-catch blocks
3. **Uncaught promise rejections** - The notification.close() and other promise-based operations weren't handling errors
4. **No diagnostic tools** - No way to debug why notifications weren't working

## Changes Made

### 1. useNotifications.ts
- ✅ Added `checkNotificationSupport()` diagnostic function
- ✅ Added comprehensive error handling to `sendNotification()`
- ✅ Added error handlers for notification.onerror events
- ✅ Added try-catch around notification.close() to prevent uncaught errors
- ✅ Improved test notification with better error handling
- ✅ Added double-check for Notification permission in `checkBusNotifications()`
- ✅ Fixed icon references (using '/front-of-bus.png' instead of just '/bus-station.png')

### 2. main.ts
- ✅ Changed service worker registration to work in both development and production
- ✅ Only run update checks in production mode
- ✅ Service worker now active in development for notification support

### 3. sw.js (Service Worker)
- ✅ Refactored development mode to keep service worker active but bypass caching
- ✅ Added push notification handlers in development mode
- ✅ Added proper error handling for notification operations
- ✅ Added error logging for debugging

### 4. MapView.vue
- ✅ Added `checkNotificationSupport()` call on mount for diagnostics
- ✅ Improved error messages in `handleNotificationToggle()`
- ✅ Added browser support check before attempting notifications
- ✅ Better user feedback with emojis and clear messages

## How to Test

1. **Clear browser cache and restart dev server**
   ```powershell
   # In TursibFrontend/Frontend directory
   npm run dev
   ```

2. **Open browser console** (F12) and look for:
   - `✅ Service Worker registered`
   - `🔍 Notification Diagnostics` output
   - `✅ Notificări activate pentru stația X`

3. **Click the bell icon** on a nearby station:
   - Should see permission prompt if not granted yet
   - Should receive a test notification immediately
   - Should receive notifications when buses approach (within 2 minutes)

4. **Check diagnostics in console**:
   ```javascript
   // In browser console:
   Notification.permission // Should be "granted"
   navigator.serviceWorker.controller // Should be an object, not null
   ```

## Expected Behavior

✅ **When activating notifications:**
1. Browser prompts for notification permission (first time only)
2. Test notification appears: "🔔 Notificări activate!"
3. Console shows: "✅ Notificări activate pentru stația [ID]"
4. Console shows: "✅ Notificare de test trimisă"

✅ **When buses approach (within 2 minutes):**
1. Notification appears: "🚌 Autobuzul Linia [X] se apropie!"
2. Body text: "Va sosi la [Station Name] în [X] minute"
3. Console shows: "🔔 Notificare trimisă pentru autobuzul [ID]"
4. Notification auto-closes after 10 seconds

✅ **Error handling:**
- No more "Uncaught (in promise) undefined" errors
- Graceful fallbacks if notifications fail
- Clear error messages in console with ❌ emoji

## Troubleshooting

### If notifications still don't work:

1. **Check browser permissions:**
   - Chrome: Settings → Privacy and security → Site settings → Notifications
   - Firefox: Settings → Privacy & Security → Permissions → Notifications
   - Make sure localhost is allowed

2. **Verify service worker:**
   ```javascript
   // In browser console
   navigator.serviceWorker.getRegistrations().then(console.log)
   ```

3. **Hard refresh:**
   - Ctrl + Shift + R (Windows/Linux)
   - Cmd + Shift + R (Mac)
   - Or clear cache and reload

4. **Check secure context:**
   - Notifications only work on HTTPS or localhost
   - Check console for "Secure Context" in diagnostics

5. **Browser compatibility:**
   - Use latest Chrome, Firefox, or Edge
   - Safari has limited notification support

## Technical Notes

- Notifications use the Web Notification API
- Service Worker handles background notifications
- Haversine formula calculates distance between bus and station
- Notifications trigger when ETA ≤ 2 minutes
- Each bus notification is sent only once (deduplicated)
- Auto-reset after 5 minutes (bus passed)

## Files Modified

1. `src/composables/useNotifications.ts` - Core notification logic
2. `src/main.ts` - Service worker registration
3. `public/sw.js` - Service worker implementation
4. `src/components/MapView.vue` - UI integration and handlers

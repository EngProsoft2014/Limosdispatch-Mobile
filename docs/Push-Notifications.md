# Mobile push notifications

Implemented against `FIREBASE-API-AR.html` and `Firebase-Mobile.postman_collection.json`, supplied by the web developer on 2026-11-09. API: `https://api.limosdispatch.com`. Firebase project: `limosdispatch`; Android/iOS application ID: `com.limosdispatch.mobile`.

## Mobile implementation

- `Plugin.Firebase.CloudMessaging` 4.0.1 with Firebase Core initialization on each platform. The existing public Android/iOS configuration files have their actual GoogleServicesJson/BundleResource build actions; the iOS resource is bundled at the application root.
- Android high-importance `ride_updates` channel, a white notification icon, Android 13+ runtime permission, and a private FCM service. Its generated component name is pinned in the manifest: recheck it when updating the plugin. The native Firebase fallback service is expected; do not add a second custom messaging service.
- iOS notification authorization, APNs registration and Firebase tap/foreground delegates. Debug uses development APNs entitlements; Release uses production. A matching Apple App ID, signing certificate/profile and APNs Firebase configuration are required on the Mac.
- A retained iOS delegate forwards Firebase events and completes native callbacks exactly once, including the silent-duplicate branch that Plugin 4.0.1 otherwise leaves incomplete. Re-evaluate the small wrapper when upgrading the plugin.
- A stable per-installation UUID. Register after workspace sign-in/restoration and every Firebase token refresh. Re-register on resume. The FCM token is neither logged nor copied to application preferences. Registration is serialized with sign-out; unregister this installation while its JWT is valid, then call the existing logout endpoint.
- The backend currently signs out ALL sessions/devices of the account at `/api/auth/logout`. A session-only endpoint is a pending web change. API integration tests discard their own in-memory JWT instead of revoking every real device.
- Device permission/registration status and an Enable notifications retry action are available in Notifications. Registration success does not prove that the backend has successfully sent a push message.

## Inbox and opening a ride

The server inbox replaces the former projection of upcoming trips. It provides paged notification history, opaque URL-encoded cursors, authoritative unread count, individual read acknowledgements and Mark all as read. Refresh happens on foreground resume, FCM, SignalR and the foreground timer. SignalR refreshes data without a second assignment announcement; FCM owns the user alert. Foreground duplicate notification IDs are suppressed and inbox rows are deduplicated by ID.

Supported event types: `ride_assigned`, `website_booking_created`, `trip_extra_requested`.

Android handles BOTH normal background FCM launcher extras and foreground local-notification taps, in OnCreate and OnNewIntent. Foreground notifications have independent PendingIntent identities, so an older notification cannot accidentally open the newest ride. iOS routes NotificationTapped through the same service.

`RideLinkService` stores only company/trip/notification identifiers until a signed-in Shell exists. A tap waits for startup restoration or sign-in, requires the same company, then fetches **GET /api/trips/{id}** for either role. The authenticated repository refreshes rejected 401 credentials once. A 404 becomes “This ride is no longer available.” Notification payloads never grant access. A successfully opened notification is marked read on the server. Newer taps supersede requests still loading.

External URL routing remains `limosdispatch://ride/TRIP_ID?company=lux`. This is an application URL scheme, not a Firebase Dynamic Link or a deployed HTTPS Universal/App Link.

## Localization and delivery limits

The notification list, system event labels, registration status and client errors have English/Arabic/Spanish resources. Custom passenger/route/database text is preserved. Background title/body language comes from the server payload; a per-device language field/server localization is still a requested backend enhancement.

The backend uses notification + data payloads. The OS handles background display. Foreground handling is configured by the SDK. Silent/Focus/Do Not Disturb, denied permissions, network restrictions and Android force-stop affect delivery; do not promise immediate or exactly-once delivery. Client notification-ID deduplication cannot prevent every banner already displayed by the OS. Server-side Android notification tags/APNs collapse identifiers can improve duplicate-display handling.

## Verification on 2026-11-09

- 97 offline checks cover payload/tenant validation, cursor escaping, JWT-derived registration ownership, current trip lookup, read acknowledgements, idempotent unregister and a single permitted registration-409 retry. They also verify that booking creation omits server-owned Extras, which the newly deployed API rejects even as an empty array.
- Android build/deployment and actual device registration succeeded on the existing Android 11 emulator. The published inbox/count endpoints were verified with authenticated read-only calls.
- A unique `MOBILE QA Push notifications` booking was assigned: the driver's `ride_assigned` event appeared. A synthetic 1-minute Extra request generated the admin `trip_extra_requested` event without approving or charging anything.
- **Cold-start ROUTING verified:** stopped the test app, confirmed it had no process, then launched the Activity with the same extras that a background FCM tap supplies. The saved session restored, the exact QA trip opened with current Enroute details, its server `isRead` became true, and unread count became zero. This was a simulated tap, not proof of real FCM transport delivery.
- **Actual FCM delivery is NOT verified:** no system notification arrived in the emulator during the observed test. The user is asking the web developer to confirm Firebase:Enabled, the service-account configuration and PushDeliveries error/status.
- iOS C#/XAML compilation succeeds on Windows with native download targets disabled for verification. Native framework linking, signing, APNs token delivery, sound and tap behavior still require macOS/Xcode and a real iPhone.

Delivery diagnostic: notification `810bc3271e8d43ef82c4034f75377737`, trip `85ccde3d-d120-4f8a-95b2-9b83c4162763`, company `lux`, event `trip_extra_requested`. Do not send service-account keys or device tokens with this diagnostic.

The server prevents deleting trips that have Extra audit entries and refuses cancellation of an already started ride through generic records. The synthetic Extra was rejected with no charge, and both QA notifications were marked read. **The exact QA trip above remains Enroute and requires authorized web-developer cleanup/archival.** It was deliberately not completed, since that would produce synthetic driver earnings. The local guarded QA state is retained until the web developer resolves that test record; never erase Extra audit data or bypass workflow protections.

## Remaining acceptance checks

1. Confirm backend Firebase sending is enabled and inspect PushDeliveries for the diagnostic ID above. A successful registration/inbox entry alone is insufficient.
2. Send a NEW QA event after server configuration is fixed; test actual FCM delivery and tapping in foreground, background and terminated state on Android and a physical iPhone.
3. Verify Android 13+ permission denial/grant, iOS permission denial/grant, token rotation, account/company rebinding, reassigned/deleted-trip 404, expired-session restoration and cross-company tap rejection.
4. Agree on per-device preferred language and session-only logout before enabling those additional backend features in the client.

References: https://firebase.google.com/docs/cloud-messaging/android/receive-messages ; https://firebase.google.com/docs/cloud-messaging/ios/receive-messages ; https://github.com/TobiasBuchholz/Plugin.Firebase/blob/development/docs/cloud_messaging.md

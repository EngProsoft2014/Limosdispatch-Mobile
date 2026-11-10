# Deployed mobile push API contract

This supersedes the proposed contract. The implementation follows the web developer's `FIREBASE-API-AR.html` and `Firebase-Mobile.postman_collection.json` received on 2026-11-09. Base URL: `https://api.limosdispatch.com`; Bearer JWT scopes tenant/user identity.

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/push/devices` | Register/rebind installation; repeat after sign-in/token refresh |
| DELETE | `/api/push/devices/{installationId}` | Unregister current installation before logout; idempotent 204 |
| GET | `/api/notifications?limit=20&cursor=...` | Server inbox; opaque cursor must be URL-encoded |
| GET | `/api/notifications/unread-count` | Authoritative unread count |
| POST | `/api/notifications/{id}/read` | Individual read acknowledgement |
| POST | `/api/notifications/read-all` | Read all current user's entries |
| GET | `/api/trips/{tripId}` | Current authorized trip details, `{trip:{...}}` |
| POST | `/api/auth/logout` | Current backend behavior revokes ALL account sessions/devices |

Registration body: `installationId` (stable UUID), `platform` (`android`/`ios`), `registrationToken` (FCM token on BOTH platforms), `appVersion`. Never submit tenant/user ownership fields. One concurrent registration 409 is retried; ambiguous booking writes are not automatically retried.

Inbox response: `items`, `nextCursor`, `unreadCount`. Notification fields: `id`, `type`, `title`, `body`, `company`, `tripId`, `extraId`, `revision`, `createdAt`, `readAt`, `isRead`. Read endpoints return `unreadCount`; read-all also returns `updated`. The badge is not derived from the number of loaded rows.

FCM events: `ride_assigned` -> chauffeur; `website_booking_created` -> authorized Admin/Dispatch; `trip_extra_requested` -> authorized reviewers. Data includes strings `type`, `company`, `tripId`, `notificationId`, `revision`, `extraId`. Background payload includes notification title/body. Android channel is `ride_updates`; iOS uses APNs alert/sound. The client fetches the ride after authentication; old notification ownership does not override current assignment/role permissions.

Pending web changes: per-installation language (`ar`, `en`, `es`) with localized background title/body; session-only logout. These are not silently assumed or sent by the current client.

Server service-account credentials stay outside the website/mobile assets. No service-account JSON, APNs private key, web Firebase config or VAPID credential is embedded in the mobile push implementation.

See [Push-Notifications.md](Push-Notifications.md) for platform setup, measured test results and the unresolved FCM delivery diagnostic.

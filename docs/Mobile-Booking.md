# Mobile booking parity

Implemented from the live web booking code (`app.js`, `reservation-checkout.js`, `tenant-enhancements.js`, `trip-extras.js`):

- Places API (New) suggestions after three characters, 300 ms debounce, per-search session token, formatted-address resolution and Google attribution.
- Google Routes driving distance, no alternative route, rejection of partial address matches and missing routes. Convert meters to miles using 1609.344; invalidate the previous distance/fare whenever the route changes.
- Company `api/data/tierPricing`: fixed fare per distance band; hours multiplied by hourly band rate. Band upper bounds are exclusive except the last band's endpoint.
- Hourly journeys require a pickup and positive hours, with no drop-off/distance requirement.
- Active chauffeur and fleet choice before save. Create an unassigned server record first, then call the authenticated assignment endpoint with its returned ID. Preserve the created ride if assignment fails so retrying does not create a duplicate booking.
- Preliminary same-date/time chauffeur/vehicle conflict validation; assignment API remains authoritative.
- Admin/Dispatch has Rides, Expenses, Earnings, Tracking, More. Driver keeps four tabs.
- Dispatch inbox includes website bookings and pending extra requests; ride details show extra type, quantity, unit, status and description.
- Passenger search uses the authenticated Customers records, searches name/normalized phone/email, auto-fills the selected contact, and stores `passengerId`, `customerId` and `bookingSource`. New passengers are saved as Individual Customers with a stable client-generated Customer ID; exact normalized name/phone duplicates are reused. Adding requires Customers Add permission. A retry after a lost response looks up the same Customer ID before posting again.
- The selected customer's cash-price summary comes from `api/reservations/quote`, including automatic customer discounts. Save rechecks the quote and persists the server price breakdown. The shared records version is refreshed after selecting/adding a passenger.
- Address results now use themed location-icon tiles, a bold primary place/street name and a secondary address line, with visible Google attribution and loading/intro states.
- One-tap lists (addresses, passengers, shared choice popups and tracking drivers) use tap actions with native item selection disabled, preventing the platform orange selected background while preserving choice checkmarks.
- Pickup/drop-off fields retain height 44 with full single-line horizontally scrollable text and a separate search icon. Swiping reads a long address; the search icon opens address selection.

The form also includes coupon checking, Booster/Infant seat quantities (whole numbers 0–20), Pending/Paid status and Zelle/Cash/Check payment methods. Quotes show fare, customer discount, coupon discount, discounted subtotal, card fees and final total. Credit Card previews are available but saving a card booking is blocked before any write: mobile card collection is not enabled. Separate corporate billing account selection and Stripe checkout remain outside this form.

English, Arabic and Spanish resources switch live with a persisted language preference and RTL for Arabic. Arabic uses bundled El Messiri regular/semibold, including native Shell tab titles. Names, email and phone have inline validation; passenger creation appears only after a nonempty search has no results.

Google HTTP calls use a separate client with no tenant bearer headers. The supplied client key requires Places API (New), Routes API and billing. Before production distribution, use platform-specific supported restrictions or an authenticated backend proxy for web-service calls; server-only keys must stay on the server.

Validation: 82 offline checks, live admin API parsing, Android emulator UI inspection and live Google suggestions/route access. A uniquely named MOBILE QA passenger was created, quoted and removed by exact Customer ID/server record ID. iOS managed code compiles on Windows; native iPhone verification requires macOS/Xcode.

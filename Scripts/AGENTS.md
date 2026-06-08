# klooie Scripts Notes

This folder contains no-argument wrappers for reusable klooie validation.

- `Test-Klooie-Web-Fast-Headless.cmd`: normal agent browser validation for the reusable klooie web host. It runs kpack packaging first, but kpack may skip work when its stamp is current.
- `Test-Klooie-Web-Fast-Headful.cmd`: visible browser version for local inspection. It also packages first if needed.
- `Test-Klooie-Web-BuiltBits-Headless.cmd`: serves existing sample app `bin\klooie.web` and runs tests without invoking kpack.
- `Test-Klooie-Web-BuiltBits-Headful.cmd`: visible browser version for existing built bits.
- The narrower Chromium/mobile scripts are quick slices for focused iteration.

Do not put consuming-app assertions here. CLAWS-specific web demo tests live in the parent repo's `Claws.Web.PlaywrightTests`.

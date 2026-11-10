LimosDispatch Mobile — .NET MAUI / XAML / MVVM
================================================
تطبيق Android وiOS للأدمن والديسباتش والسائق، متصل بـ api.limosdispatch.com.
الإصدار الحالي في المصدر: 1.1 / build 2. هذا مستودع تطوير؛ لم تُعتمد نسخة متجر بعد.

الوظائف
--------
الرحلات ومراحلها، الإنشاء والتعديل والإلغاء والإسناد، طلبات Extra ومراجعتها،
المصروفات والمرفقات الاختيارية، الأرباح، التتبع الحي وسجل المسارات، إشعارات
FCM وروابط تفاصيل الرحلات، وإكمال الرحلة دون اتصال مع المزامنة المؤجلة.
اللغات: الإنجليزية والعربية والإسبانية، مع RTL وخط El Messiri في العربية.
تختفي بطاقات المزامنة المكتملة من قائمة المزامنة بعد 24 ساعة من اكتمالها،
مع بقاء المعلّق وما يحتاج مراجعة. هذا إخفاء من القائمة وليس حذفًا للسجل.

المتطلبات والتشغيل
------------------
1. .NET 10 SDK وMAUI workloads وAndroid SDK وJDK المتوافقان.
2. افتح LimosDispatch.sln، أو افتح مجلد المشروع في VS Code وثبت الامتدادات المقترحة.
3. اضبط مسارات SDK/JDK على جهازك في إعدادات VS Code المحلية عند الحاجة.
4. من Run and Debug اختر LimosDispatch · Android Emulator، ثم Run أو F5.
   مهمة maui: Build يوفرها امتداد .NET MAUI؛ زر Code Runner ليس مشغّل MAUI.
5. يمكن البناء عبر CLI:
   dotnet build src/LimosDispatch.Mobile/LimosDispatch.Mobile.csproj -f net10.0-android -p:MobileTargetFrameworks=net10.0-android
6. iOS يحتاج Mac وXcode وApple signing صالحين:
   dotnet build src/LimosDispatch.Mobile/LimosDispatch.Mobile.csproj -f net10.0-ios -p:MobileTargetFrameworks=net10.0-ios
7. سجّل الدخول بكود الشركة ثم بيانات حسابك. لا توجد حسابات أو كلمات مرور افتراضية.

لمسار بناء قصير على Windows يمكن تمرير:
   -p:LimosBuildRoot=C:/path/to/work/ld
لا تعتمد ملفات التشغيل المشتركة على موقع E: لجهاز المطوّر.

التهيئة والأمان
--------------
ملفا Firebase داخل Platforms هما تهيئة عميل للتطبيق، وليسا Service Account.
مفاتيح Google الموجودة في التطبيق مفاتيح عميل مرتبطة بالخدمات والحزمة؛ يجب
ضبط قيودها في Google Cloud وفق docs/Maps-Setup.txt قبل تغيير الحزمة أو التوقيع.
لا تضع Service Account JSON أو APNs p8 أو شهادات توقيع خاصة أو tokens أو
كلمات مرور في المستودع. هذه الملفات وملفات البناء والبيانات المحلية مستبعدة.
لشركة/مشروع Firebase آخر، استبدل تهيئة العميل ومفاتيح Google بالتهيئة المناسبة.

التحقق — 10 نوفمبر 2026
-----------------------
dotnet run --project tests/LimosDispatch.Core.Checks
  157 فحصًا + 28 فحص عقد مالي بمحاكاة HTTP.
dotnet run --project tests/LimosDispatch.Tracking.Checks
  14 فحصًا لخدمة التتبع الفعلية مع محاكاة الجهاز والناشر.
dotnet run --project tests/LimosDispatch.Refresh.Checks
  8 فحوص لتحديث الصفحات وإلغاء الطلبات.
dotnet run --project tests/LimosDispatch.OfflineSync.Checks
  34 فحصًا مع SQLite مشفر حقيقي وAPI محاكى، تشمل إخفاء المكتمل بعد 24 ساعة.

نجح بناء Android وتثبيته على المحاكي دون أخطاء أو تحذيرات.
اختُبر إنهاء رحلة MOBILE QA دون شبكة ثم قتل التطبيق وإعادة تشغيله، ورفع
الإنهاء ونقاط GPS المؤجلة للسيرفر. نجحت اختبارات الإقرار ومنع التكرار المنشورة.
اختُبرت نغمة FCM في الخلفية على Android 1.1 وإصدار تجريبي 1.2 ثم أُعيد إلى 1.1.
اختبارات التكامل المذكورة استخدمت سجلات MOBILE QA صناعية داخل lux، دون رسوم.
لا تشغّل اختبارات الكتابة على بيانات عميل حقيقية دون نطاق اختبار مصرح به.

حدود التحقق
-----------
لم يُنفّذ بناء أو اختبار عملي لـiOS بعد. يلزم اختباره على iPhone قبل النشر.
المزامنة المؤجلة تعمل عند فتح التطبيق بالحساب الأصلي ومع الإنترنت، ضمن
نافذة السيرفر الحالية 7 أيام. لا تفترض رفعًا والتطبيق مقتول أو استرجاع بيانات
حُذفت من الجهاز. حالات تعارض الإسناد/الإلغاء تبقى للمراجعة ولا تُعتمد تلقائيًا.
إخفاء المزامنات الناجحة لا يمحو الإقرارات المحلية؛ سياسة تنظيفها منفصلة.

البنية والتوثيق
---------------
src/LimosDispatch.Core: Models, Constants, Helpers, GenericRepository, Services.
src/LimosDispatch.Mobile: XAML ContentPages, ViewModels, Controls, Converters,
Helpers, Services, وPlatforms/Android وPlatforms/iOS.
ملفات Core مشتركة المصدر مع الموبايل، ومكتبة مستقلة لاختبارات الأعمال.

ابدأ بالتقارير الحالية:
  docs/OFFLINE-SYNC-IMPLEMENTATION-RETEST-AR.html
  docs/TRACKING-SYSTEM-DOCUMENTATION-AR.html
  docs/CUSTOM-NOTIFICATION-SOUND-AR.html
  docs/Maps-Setup.txt
بقية تقارير QA مؤرخة وتصف حالة المشروع وقت الفحص؛ لا تُعد كل ملاحظة تاريخية
عطلًا قائمًا بعد التقارير الأحدث.

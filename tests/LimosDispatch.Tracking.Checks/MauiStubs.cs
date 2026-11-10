global using LimosDispatch.Core.Models;
global using LimosDispatch.Core.Services;
global using TrackingChecks;
namespace TrackingChecks;
// Device adapters only: the production TrackingService and Core repository execute unchanged.
public enum PermissionStatus { Granted }
public static class Permissions { public sealed class LocationWhenInUse {} public static Task<PermissionStatus> RequestAsync<T>() => Task.FromResult(PermissionStatus.Granted); }
public enum GeolocationAccuracy { Best }
public sealed record GeolocationRequest(GeolocationAccuracy Accuracy, TimeSpan Timeout);
public sealed class Geolocation { public static Geolocation Default { get; }=new(); public bool IsEnabled=>true; public TestLocation Fix {get;set;}=new(); public Task<TestLocation?> GetLocationAsync(GeolocationRequest request)=>Task.FromResult<TestLocation?>(Fix); }
public sealed class SecureStorage { public static SecureStorage Default { get; }=new(); public Dictionary<string,string> Values { get; }=[]; public Task<string?> GetAsync(string key)=>Task.FromResult(Values.GetValueOrDefault(key)); public Task SetAsync(string key,string value){Values[key]=value;return Task.CompletedTask;} public bool Remove(string key)=>Values.Remove(key); }
public static class Preferences { public static PreferenceStore Default { get; }=new(); public sealed class PreferenceStore { public bool Get(string key,bool fallback)=>fallback; } }
public static class MainThread { public static void BeginInvokeOnMainThread(Action action)=>action(); }
public static class DeviceDisplay { public static Display Current { get; }=new(); public sealed class Display { public bool KeepScreenOn { get;set; } } }

public sealed class TestLocation { public DateTimeOffset Timestamp {get;set;}=DateTimeOffset.UtcNow.AddSeconds(-10);public double Latitude {get;set;}=29.98;public double Longitude {get;set;}=-95.34;public double? Accuracy {get;set;}=5; }

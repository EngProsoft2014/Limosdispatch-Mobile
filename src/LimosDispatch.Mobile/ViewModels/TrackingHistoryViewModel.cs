using System.Collections.ObjectModel;
using LimosDispatch.Core.Helpers;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class TrackingHistoryViewModel(DispatchService dispatch):BaseViewModel
{
    private List<Driver> all=[];private string query="";
    public string Query {get=>query;set{Set(ref query,value);Filter();}}
    public ObservableCollection<Driver> Drivers {get;}=[];
    private void Filter(){Drivers.Clear();foreach(var d in all.Where(d=>string.IsNullOrWhiteSpace(Query)||(d.Name+" "+d.Vehicle).Contains(Query.Trim(),StringComparison.CurrentCultureIgnoreCase)))Drivers.Add(d);}
    public override Task LoadAsync()=>Run(async()=>{all=(await dispatch.DriversAsync()).Where(d=>!string.IsNullOrEmpty(d.TrackingId)).OrderBy(d=>d.Name).ToList();Filter();});
}
public sealed class DriverHistoryViewModel(DispatchService dispatch):BaseViewModel
{
    public Driver Driver {get;set;}=new();
    private DateTime from=DateTime.Today,to=DateTime.Today;
    public DateTime From {get=>from;set=>Set(ref from,value);}
    public DateTime To {get=>to;set=>Set(ref to,value);}
    public DateTime Today=>DateTime.Today;
    public ObservableCollection<Trip> Trips {get;}=[];
    public DriverHistoryViewModel Configure(Driver driver){Driver=driver;Raise(nameof(Driver));RefreshCommand=new(LoadAsync);return this;}
    public override Task LoadAsync()=>Run(async()=>{
        if(From.Date>To.Date)throw new InvalidOperationException("The start date must be before the end date.");
        var trips=await dispatch.TripsAsync();Trips.Clear();
        foreach(var t in trips.Trips.Where(t=>t.DriverId==Driver.TrackingId&&RoutePlayback.InDateRange(t,From,To)).OrderByDescending(t=>t.PickupAt))Trips.Add(t);
    });
}
public sealed record PlaybackEvent(string Title,string Detail,long At)
{
    public string Time=>DateTimeOffset.FromUnixTimeMilliseconds(At).ToLocalTime().ToString("HH:mm:ss");
}
public sealed class RoutePlaybackViewModel(TrackingArchiveService archive,IGenericRepository api):BaseViewModel
{
    public Trip Trip {get;set;}=new();
    public IReadOnlyList<GpsPoint> Points {get;private set;}=[];
    public ObservableCollection<PlaybackEvent> Events {get;}=[];
    public event Action? Loaded;
    private double position;private bool playing,follow=true,showEvents;private string speed="15×";
    public double Position {get=>position;set{if(Set(ref position,Math.Clamp(value,0,Maximum))){Raise(nameof(CurrentTime));Raise(nameof(ProgressText));PositionChanged?.Invoke();}}}
    public event Action? PositionChanged;
    public double Maximum=>Points.Count<2?1:Math.Max(1,Points[^1].CapturedAt-Points[0].CapturedAt);
    public long CurrentTimestamp=>Points.Count==0?0:Points[0].CapturedAt+(long)Position;
    private static string Time(long t)=>t<=0?"—":DateTimeOffset.FromUnixTimeMilliseconds(t).ToLocalTime().ToString("dd MMM · HH:mm:ss");
    public string CurrentTime=>Time(CurrentTimestamp);
    public string StartTime=>Points.Count==0?"—":Time(Points[0].CapturedAt);
    public string EndTime=>Points.Count==0?"—":Time(Points[^1].CapturedAt);
    public string Summary=>LocalizationService.Format("{0} locations · {1:0.0} km recorded",Points.Count,RoutePlayback.DistanceKm(Points));
    public string ProgressText=>TimeSpan.FromMilliseconds(Position).ToString(@"hh\:mm\:ss")+" / "+TimeSpan.FromMilliseconds(Points.Count<2?0:Maximum).ToString(@"hh\:mm\:ss");
    public bool HasPoints=>Points.Count>0;
    public bool CanPlay=>Points.Count>1;
    public bool Playing {get=>playing;set{Set(ref playing,value);Raise(nameof(PlayText));}}
    public string PlayText=>LocalizationService.Text(Playing?"Pause":"Play");
    public bool Follow {get=>follow;set=>Set(ref follow,value);}
    public bool ShowEvents {get=>showEvents;set=>Set(ref showEvents,value);}
    public IReadOnlyList<string> Speeds {get;}=["1×","5×","15×","60×"];
    public string Speed {get=>speed;set=>Set(ref speed,value);}
    public void Tick(double elapsedMs){if(!Playing)return;Position+=elapsedMs*double.Parse(Speed.TrimEnd('×'),System.Globalization.CultureInfo.InvariantCulture);if(Position>=Maximum)Playing=false;}
    public void Toggle(){if(!CanPlay)return;if(Position>=Maximum)Position=0;Playing=!Playing;}
    public override Task LoadAsync()=>Run(async()=>{
        Playing=false;Points=[];Events.Clear();Position=0;Raise(string.Empty);
        var response=await api.GetAsync<PushApi.TripResponse>("api/trips/"+Uri.EscapeDataString(Trip.Id));Trip=response.Trip??throw new InvalidOperationException("Journey no longer exists.");
        Points=await archive.LocationsAsync(Trip.Id);
        var events=Trip.History.Select(e=>new PlaybackEvent(e.StageLabel,"",e.At)).ToList();
        foreach(var e in Trip.Extras){events.Add(new(LocalizationService.Format("Extra {0}",LocalizationService.Text(e.Type)),e.Description,e.RequestedAt));if(e.ReviewedAt is {} at)events.Add(new(e.Status,e.ReviewNote,at));}
        foreach(var e in events.Where(e=>e.At>0&&e.At<=253402300799999).OrderBy(e=>e.At))Events.Add(e);
        position=0;Raise(string.Empty);Loaded?.Invoke();
    });
}

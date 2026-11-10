using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
namespace LimosDispatch.Core.Models;
public sealed class NullableMoneyConverter:JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader r,Type t,JsonSerializerOptions o)=>r.TokenType==JsonTokenType.Null?null:r.TokenType==JsonTokenType.Number?r.GetDecimal():r.TokenType==JsonTokenType.String&&string.IsNullOrWhiteSpace(r.GetString())?null:r.TokenType==JsonTokenType.String&&decimal.TryParse(r.GetString(),NumberStyles.Number,CultureInfo.InvariantCulture,out var v)?v:throw new JsonException("Invalid financial number");
    public override void Write(Utf8JsonWriter w,decimal? v,JsonSerializerOptions o){if(v is {} n)w.WriteNumberValue(n);else w.WriteNullValue();}
}
public sealed class FlexibleBooleanConverter:JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader r,Type t,JsonSerializerOptions o)=>r.TokenType==JsonTokenType.True||r.TokenType==JsonTokenType.String&&bool.TryParse(r.GetString(),out var b)&&b;
    public override void Write(Utf8JsonWriter w,bool v,JsonSerializerOptions o)=>w.WriteBooleanValue(v);
}
public sealed class ExpenseLookups {public List<FleetVehicle> Vehicles{get;set;}=[];public List<Driver> Drivers{get;set;}=[];public List<string> Categories{get;set;}=[];}
public sealed class FinancialReport
{
    public string From{get;set;}="";public string To{get;set;}="";public decimal Revenue{get;set;}public decimal Operating{get;set;}public decimal UnpostedMaintenance{get;set;}public decimal DriverWages{get;set;}public decimal NetProfit{get;set;}public bool Provisional{get;set;}public List<string> Issues{get;set;}=[];public string Note{get;set;}="";
    [JsonIgnore]public decimal Margin=>Revenue==0?0:100*NetProfit/Revenue;
}
public sealed class WorkspaceEnvelope {public long Version{get;set;}public Dictionary<string,JsonElement> Data{get;set;}=[];}
public sealed record CategoryResult(List<string> Categories);
public sealed record ExtraReviewRequest(string TripId,string ExtraId,string Decision,decimal Amount,bool CustomerConfirmed,string Note);

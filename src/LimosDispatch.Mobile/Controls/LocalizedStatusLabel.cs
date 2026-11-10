namespace LimosDispatch.Mobile.Controls;

// Bind each status to the active resource dictionary, so recycled rows also update on language changes.
public sealed class LocalizedStatusLabel : Label
{
    public static readonly BindableProperty StatusProperty=BindableProperty.Create(nameof(Status),typeof(string),typeof(LocalizedStatusLabel),"",propertyChanged:(view,_,_)=>((LocalizedStatusLabel)view).UpdateStatus());
    public string Status {get=>(string)GetValue(StatusProperty);set=>SetValue(StatusProperty,value);}
    public LocalizedStatusLabel(){SetDynamicResource(StyleProperty,"BaseLabel");}
    private void UpdateStatus()
    {
        var key=(Status??"").Trim().ToLowerInvariant();
        var english=key switch
        {
            "unassigned"=>"Unassigned","confirmed"=>"Confirmed",
            "enroute" or "on the way"=>"On the way",
            "arrived" or "at the pickup location"=>"At the pickup location",
            "onboard" or "passenger on board"=>"Passenger on board",
            "completed" or "finished"=>"Finished",
            "cancelled" or "canceled"=>"Cancelled",
            "no-show" or "no show" or "noshow"=>"No-show",
            "pending"=>"Pending","approved"=>"Approved","rejected"=>"Rejected",
            _=>null
        };
        if(english is not null)SetDynamicResource(TextProperty,LocalizationService.Key(english));
        else {RemoveDynamicResource(TextProperty);Text=Status;}
    }
}

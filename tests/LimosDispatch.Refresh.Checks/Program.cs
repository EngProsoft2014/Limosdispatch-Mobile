using LimosDispatch.Mobile.ViewModels;
using LimosDispatch.Core.Helpers;
int checks=0;void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);checks++;}
var vm=new Probe();var feedback=new List<string>();UserFeedback.Presenter=s=>{feedback.Add(s);return Task.CompletedTask;};
var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int executions=0;var busyTransitions=new List<bool>();vm.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.IsBusy))busyTransitions.Add(vm.IsBusy);};
var background=vm.Execute(async()=>{executions++;await release.Task;},false);
Check(!vm.IsBusy&&busyTransitions.Count==0,"Automatic refresh never exposes a busy indicator");
await vm.Execute(()=>{executions++;return Task.CompletedTask;},false);var manual=vm.Execute(()=>{executions++;return Task.CompletedTask;},true);
Check(executions==1&&!manual.IsCompleted,"A user action waits while automatic requests remain coalesced");release.SetResult();await background;await manual;Check(executions==2,"The waiting user action executes after the silent refresh");busyTransitions.Clear();
await vm.Execute(()=>Task.CompletedTask,true);Check(busyTransitions.SequenceEqual(new[]{true,false}),"Manual refresh still exposes and clears its busy indicator");
await vm.Execute(()=>throw new HttpRequestException(),false);Check(!vm.IsBusy&&vm.HasError&&feedback.Count==1,"Background failure remains visible without a spinner or unhandled exception");
await vm.Execute(()=>Task.CompletedTask,false);Check(!vm.HasError&&!vm.IsBusy,"A successful retry clears the previous error");
using var canceled=new CancellationTokenSource();canceled.Cancel();await vm.Execute(()=>Task.FromCanceled(canceled.Token),false);Check(feedback.Count==1&&!vm.IsBusy,"Leaving the page can cancel a background request without an error notification");
await vm.Execute(()=>Task.CompletedTask,true);Check(!vm.IsBusy,"Cancellation releases the request gate for the next user action");Console.WriteLine($"{checks} production BaseViewModel refresh checks passed.");
sealed class Probe:BaseViewModel {public Task Execute(Func<Task> action,bool busy)=>Run(action,busy);}

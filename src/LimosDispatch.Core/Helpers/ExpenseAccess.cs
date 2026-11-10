using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
public static class ExpenseAccess
{
 public static bool CanAccess(UserProfile user,string? owner)=>!user.IsDriver||user.CanDispatch||string.Equals(user.Name.Trim(),owner?.Trim(),StringComparison.OrdinalIgnoreCase);
 public static void Demand(UserProfile user,string? owner){if(!CanAccess(user,owner))throw new UnauthorizedAccessException("Your account does not have permission for this action.");}
}

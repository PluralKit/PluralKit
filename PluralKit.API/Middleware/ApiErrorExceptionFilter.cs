using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using PluralKit.API.Models;

namespace PluralKit.API.Middleware
{
    public class ApiErrorExceptionFilter: IActionFilter, IOrderedFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (!(context.Exception is ApiErrorException exc)) 
                return;
            
            context.Result = new ObjectResult(exc.Error) {StatusCode = (int) exc.StatusCode};
            context.ExceptionHandled = true;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
        }

        public int Order => int.MaxValue - 10;
    }
}
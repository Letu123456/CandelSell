using Microsoft.AspNetCore.Mvc;
using static SADA.Core.Constants.SD;

namespace SADA.Web.Areas.Client.Controllers
{
    [Area("Client")]
    public class OrderHistoryController : Controller
	{
		
		private readonly IUnitOfWork _unitOfWorks;

		//[BindProperty]
		//public OrderManagementVM OrderManagementVM { get; set; }
		//#endregion

		#region Constructor(s)
		public OrderHistoryController(IUnitOfWork unitOfWork)
		{
			_unitOfWorks = unitOfWork;
		}
		#endregion


		public IActionResult Index()
		{
			IEnumerable<OrderHeader>? orderslist = null;
			
		   orderslist = _unitOfWorks.OrderHeader.GetAll("ApplicationUser", o => o.Id, SD.Descending,criteria: o=>o.ApplicationUserId == HttpContext.Session.GetObject<ApplicationUser>(SD.SessionLoggedUser).Id);
			
			return View(orderslist);
		}
	}
}

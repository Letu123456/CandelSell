using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Diagnostics;
using System.Linq.Expressions;

namespace SADA.Web.Areas.Client.Controllers;

[Area("Client")]
public class HomeController : Controller
{
    private readonly IUnitOfWork _unitOfWorks;

    public HomeController(IUnitOfWork unitOfWork)
    {
        _unitOfWorks = unitOfWork;
    }

    public IActionResult Index(string? search,int? categoryId)
    {
        IEnumerable<Product> productsList;
        IEnumerable<Category> categoriesList;

        if (!string.IsNullOrEmpty(search)&&categoryId==null)
        {
            Expression<Func<Product, bool>> criteria = p =>
                p.Name.Contains(search) ;

            productsList = _unitOfWorks.Product.GetAll(
                includeProperties: "Category",
                orderBy: p => p.Id,
                orderByDirection: SD.Descending,
                criteria: criteria);
        }
        else if (categoryId != null) 
        {
            Expression<Func<Product, bool>> criteria = p =>
                    p.Category.Id == categoryId ;

            productsList = _unitOfWorks.Product.GetAll(
                includeProperties: "Category",
                orderBy: p => p.Id,
                orderByDirection: SD.Descending,
                criteria: criteria);
        }else
        {
            productsList = _unitOfWorks.Product.GetAll(
                includeProperties: "Category",
                orderBy: p => p.Id,
                orderByDirection: SD.Descending);
        }
        
        categoriesList = _unitOfWorks.Category.GetAll(orderBy: p => p.Id,
                orderByDirection: SD.Descending);

        var viewModel = new ProductCategory
        {
            Products = productsList,
            Categories = categoriesList
        };

        return View(viewModel);
    }

    public IActionResult Category()
    {
        
            IEnumerable<Category> categoryList = _unitOfWorks.Category.GetAll(
                      
                       orderBy: p => p.Id, orderByDirection: SD.Descending
                       );
        

    

       
        return View(categoryList);
}
//public IActionResult Tinhdau()
//{


//    IEnumerable<Product> productsList = _unitOfWorks.Product.GetAll(
//        includeProperties: "Category",
//        orderBy: p => p.Id, orderByDirection: SD.Descending, criteria: p => p.CategoryId == 1
//        );
//    return View(productsList);
//}

[HttpGet]
    public IActionResult Details(int productId)
    {
        if (HttpContext.Session.GetObject<ApplicationUser>(SD.SessionLoggedUser) == null)
        {
            TempData["Message"] = "Please complete the registration to proceed.";
            return RedirectToPage("/Account/Register", new {area="Identity"});
        }

        ShoppingCart obj = new()
        {
            Count = 1,
            ProductId = productId,
            UserId = HttpContext.Session.GetObject<ApplicationUser>(SD.SessionLoggedUser).Id,
        Product = _unitOfWorks.Product.GetFirstOrDefault(o => o.Id== productId, includeProperties: "Category")
        };

        return View(obj);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize] //only logged user can do it
    public IActionResult Details(ShoppingCart obj)
    {
        if (!ModelState.IsValid)
            return View(obj);

        if(obj.Color == null && obj.Size == null) {

            var car = _unitOfWorks.ShoppingCart.GetFirstOrDefault(criteria:
                p => p.ProductId == obj.ProductId);

            if (car is null)
            {
                //added for first time
                _unitOfWorks.ShoppingCart.Add(obj);
                _unitOfWorks.Save();
                //update session value for cart counts
                HttpContext.Session.IncrementValue(SD.SessionCart, 1);
            }
            else
            {
                _unitOfWorks.ShoppingCart.IncrementCount(car, obj.Count);
                _unitOfWorks.Save();
            }
        }
        else {
            var cart = _unitOfWorks.ShoppingCart.GetFirstOrDefault(criteria:
            p => p.ProductId == obj.ProductId
            && p.Color == obj.Color && p.Size == obj.Size
        );

            if (cart is null)
            {
                //added for first time
                _unitOfWorks.ShoppingCart.Add(obj);
                _unitOfWorks.Save();
                //update session value for cart counts
                HttpContext.Session.IncrementValue(SD.SessionCart, 1);
            }
            else
            {
                _unitOfWorks.ShoppingCart.IncrementCount(cart, obj.Count);
                _unitOfWorks.Save();
            }
        }
       
        

        return RedirectToAction(nameof(Index));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
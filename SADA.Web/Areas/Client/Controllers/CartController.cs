using Microsoft.AspNetCore.Mvc.Rendering;
using Net.payOS.Types;
using Net.payOS;
using Stripe.Checkout;
using Twilio.Http;
using Twilio.TwiML.Messaging;


namespace SADA.Web.Areas.Client.Controllers
{
    
    [Area("Client")]
    
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWorks;
        private readonly ISmsSender _SmsSender;
        private readonly ApplicationUser _loggedUser;
        private readonly PaymentController _paymentController;
        private readonly PayOS _payOS;
        private readonly IURLHelper _urlHelper;
        public CartController(IUnitOfWork unitOfWork,
            ISmsSender smsSender,
            IHttpContextAccessor HttpContextAccessor,
            IURLHelper urlHelper,
            PayOS payOS,
            IURLHelper uRLHelper)
        {
            _unitOfWorks = unitOfWork;
            _SmsSender = smsSender;
            _loggedUser = HttpContextAccessor.HttpContext.Session.GetObject<ApplicationUser>(SD.SessionLoggedUser);
            _paymentController = new PaymentController(urlHelper);
            _payOS = payOS;
            _urlHelper = uRLHelper;

        }

        [BindProperty] //for post method
        public ShoppingCartVM ShoppingCartVM { get; set; }

        //---------------------------------- Methods---------------------------------------------------
        [HttpGet]
        public IActionResult Index()
        {
            ShoppingCartVM = UploadCartFromDb();
            return View(ShoppingCartVM);
        }

        [HttpGet]
        public IActionResult Summary()
        {
            ShoppingCartVM = UploadCartFromDb();
            ShoppingCartVM.OrderHeader.Name = _loggedUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = _loggedUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress = _loggedUser.StreetAddress;

            ShoppingCartVM.PaymentMethod = _unitOfWorks.PaymentMethod.GetAll().Select(i => new SelectListItem
            {
                Text = i.Name,
                Value = i.Id.ToString(),
            });

            ShoppingCartVM.Governorates = _unitOfWorks.Governorate.GetAll().Select(i => new SelectListItem
            {
                Text = i.Name,
                Value = i.Id.ToString(),
            });

            ShoppingCartVM.Cities = _unitOfWorks.City.GetAll().Select(i => new SelectListItem
            {
                Text = i.Name,
                Value = i.Id.ToString(),
            });

            return View(ShoppingCartVM);
        }

        [HttpPost, ActionName("Summary"), ValidateAntiForgeryToken]
        public async Task<IActionResult> SummaryPostAsync()
        {
            //OrderHeader
            ShoppingCartVM.OrderHeader.ApplicationUserId = _loggedUser.Id;

            ShoppingCartVM.OrderHeader.OrderStatus = SD.Status.Pending.ToString();

            ShoppingCartVM.OrderHeader.PaymentStatus = SD.Status.Pending.ToString();

            //OrderDetail
            ShoppingCartVM.OrderHeader.Items = new List<OrderDetail>();
            ShoppingCartVM.ListCart = CollectOrderItems();
            foreach (var item in ShoppingCartVM.ListCart)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId = item.ProductId,
                    Count = item.Count,
                    Price = item.Price
                };
                ShoppingCartVM.OrderHeader.Items.Add(orderDetail);
            }

            _unitOfWorks.OrderHeader.Add(ShoppingCartVM.OrderHeader);
            _unitOfWorks.Save();
            var orderId = ShoppingCartVM.OrderHeader.Id;
            HttpContext.Session.SetString("OrderId", orderId.ToString());
            if (orderId == 0)
            {
                return View();
            }
            if (ShoppingCartVM.OrderHeader.PaymentMethodId == SD.PaymentByCardID)
            {

               
                    int orderCode = int.Parse(DateTimeOffset.Now.ToString("ffffff"));

                    List<ItemData> items = new List<ItemData>();
                    foreach (var cartItem in ShoppingCartVM.ListCart)
                    {
                        var item = new ItemData
                        (
                            cartItem.Product.Name,
                            cartItem.Count,
                            (int)(cartItem.Price * 100));
                        items.Add(item);
                    }
                    
                    double totalAmount = ShoppingCartVM.OrderHeader.OrderTotal;
                    PaymentData paymentData = new PaymentData(
                          orderCode,
                          (int)totalAmount,
                          "Thanh toan don hang",
                           items,
                         _urlHelper.Url($"cancel"),
                           _urlHelper.Url($"Client/Cart/OrderConfirmation")
    );
                    CreatePaymentResult createPayment = await _payOS.createPaymentLink(paymentData);

                    return Redirect(createPayment.checkoutUrl);
                
                //catch (System.Exception exception)
                //{
                //    Console.WriteLine(exception);
                //    return Redirect("https://localhost:7136/");
                //}
                //stripe setting
                // Session session = await CheckoutByStripe(ShoppingCartVM.ListCart);

                // await CheckoutByStripe(ShoppingCartVM.ListCart);
                // _unitOfWorks.OrderHeader.UpdateStripePaymentID(ShoppingCartVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
                // _unitOfWorks.Save();

                // Response.Headers.Add("Location", session.Url);
                //return new StatusCodeResult(303);
            }
            else if (ShoppingCartVM.OrderHeader.PaymentMethodId == SD.PaymentByCashID)
            {
                return RedirectToAction("OrderConfirmation");
                //OrderConfirmationAsync(ShoppingCartVM.OrderHeader.Id);
            }

            return View();
        }

        [HttpGet("Client/Cart/OrderConfirmation")]
        public async Task<IActionResult> OrderConfirmationAsync()
        {
            var orderIdString = HttpContext.Session.GetString("OrderId");
            int orderId = int.Parse(orderIdString);
            OrderHeader orderHeader = _unitOfWorks.OrderHeader.GetById(orderId);
            if (orderHeader == null)
            {

                return NotFound();
            }
            //if (orderHeader.PaymentMethodId == SD.PaymentByCardID)
            //{
            //    //check the stripe status
            //    var service = new SessionService();
            //    Session session = service.Get(orderHeader.SessionId);
            //    if (session.PaymentStatus.ToLower() != "paid")
            //    {
            //        return RedirectToAction("Summary");
            //    }

            //}

            _unitOfWorks.OrderHeader.UpdateStatus(orderId, SD.Status.Approved.ToString(), SD.Status.Approved.ToString());
            _unitOfWorks.Save();

            //remove shopping cart
            List<ShoppingCart> ListCart = _unitOfWorks.ShoppingCart.GetAll(
                includeProperties: "Product",
                criteria: c => c.UserId == orderHeader.ApplicationUserId
                ).ToList();

            _unitOfWorks.ShoppingCart.RemoveRange(ListCart);
            _unitOfWorks.Save();

            //clear session value for cart
            HttpContext.Session.Remove(SD.SessionCart);

            //send sms message
            await _SmsSender.SendSMSAsync(orderHeader.PhoneNumber, $"Order Placed on Candel Bless. Your OrderID:{orderHeader.Id}");

            return View(orderId);
        }

        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWorks.ShoppingCart.GetById(cartId);

            _unitOfWorks.ShoppingCart.IncrementCount(cartFromDb, 1);

            _unitOfWorks.Save();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWorks.ShoppingCart.GetById(cartId);
            if (cartFromDb.Count > 1)
            {
                _unitOfWorks.ShoppingCart.DecrementCount(cartFromDb, 1);
            }
            else //delete
            {
                _unitOfWorks.ShoppingCart.Remove(cartFromDb);
                HttpContext.Session.DecrementValue(SD.SessionCart, 1);
            }
            _unitOfWorks.Save();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWorks.ShoppingCart.GetById(cartId);

            _unitOfWorks.ShoppingCart.Remove(cartFromDb);

            _unitOfWorks.Save();

            HttpContext.Session.DecrementValue(SD.SessionCart, 1);

            return RedirectToAction(nameof(Index));
        }

        //----------------------------------Helper Methods-------------------------------------------------
        private ShoppingCartVM UploadCartFromDb()
        {
            //get cart
            ShoppingCartVM ShoppingCartVM = new()
            {
                ListCart = CollectOrderItems(),
                OrderHeader = new()
            };
            //calculate total
            foreach (var item in ShoppingCartVM.ListCart)
            {
                ShoppingCartVM.OrderHeader.OrderTotal += (item.Price * item.Count);
            }

            return ShoppingCartVM;
        }

        private IEnumerable<ShoppingCart> CollectOrderItems()
        {
            return _unitOfWorks.ShoppingCart.GetAll(
                includeProperties: "Product",
                criteria: c => c.UserId == _loggedUser.Id);
        }

        private async Task<IActionResult> CheckoutByStripe(IEnumerable<ShoppingCart> CartList)
        {
            var options = _paymentController.Stripe(
                            successUrl: $"client/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
                            cancelUrl: "client/cart/index");

            foreach (var item in CartList)
            {
                var sessionLineItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)item.Price * 100,
                        Currency = "egp",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Name,
                        },

                    },
                    Quantity = item.Count,
                };
                options.LineItems.Add(sessionLineItem);
            }

            try
            {
                int orderCode = int.Parse(DateTimeOffset.Now.ToString("ffffff"));

                List<ItemData> items = new List<ItemData>();
                foreach (var cartItem in ShoppingCartVM.ListCart)
                {
                    var item = new ItemData
                    (
                        cartItem.Product.Name,
                        cartItem.Count,
                        (int)(cartItem.Price * 100));
                    items.Add(item);
                }
                double totalAmount = ShoppingCartVM.OrderHeader.OrderTotal;
                PaymentData paymentData = new PaymentData(
    orderCode,
    (int)totalAmount,
    "Thanh toan don hang",
    items,
    "https://localhost:3002/cancel",
    "https://localhost:3002/success"
);
                CreatePaymentResult createPayment = await _payOS.createPaymentLink(paymentData);

                return Redirect(createPayment.checkoutUrl);
            }
            catch (System.Exception exception)
            {
                Console.WriteLine(exception);
                return Redirect("https://localhost:3002/");
            }


        }


        [HttpGet("/cancel")]
        public IActionResult Cancel()
        {
            // Trả về trang HTML có tên "MyView.cshtml"
            return View("cancel");
        }


        [HttpGet("/success")]
        public IActionResult Success(int orderId)
        {
            // Trả về trang HTML có tên "MyView.cshtml"
            return RedirectToAction("OrderConfirmation", new { id = orderId });
        }
    }
}

using AutoMapper;
using E_commerce.Services.ShoppingCartAPI.Data;
using E_commerce.Services.ShoppingCartAPI.Models;
using E_commerce.Services.ShoppingCartAPI.Models.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace E_commerce.Services.ShoppingCartAPI.Controllers
{
    [Route("api/cart")]
    [ApiController]
    public class CartAPIController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private ResponseDto _response; // Plain Object, Not Injected
        private IMapper _mapper;

        //  Sample Usage: var result = _mapper.Map<DestinationClass>(sourceObject);
        public CartAPIController(IMapper mapper, ApplicationDbContext db)
        {
            _response = new ResponseDto();
            _mapper = mapper;
            _db = db;
        }

        [HttpGet("GetCart/{userId}")]
        public async Task<ResponseDto> GetCart(string userId) // userId is a guid
        {
            try
            {
                CartDto cart = new()
                {
                    // pupulate the cart header
                    CartHeader = _mapper.Map<CartHeaderDto>(_db.CartHeaders.First(u => u.UserId == userId))

                };
                // populate the cart details
                cart.CartDetails = _mapper.Map<IEnumerable<CartDetailsDto>>
                    (_db.CartDetails.Where(u => u.CartHeaderId == cart.CartHeader.CartHeaderId));

                // populate cartToatl
                foreach(var item in cart.CartDetails)
                {
                    cart.CartHeader.CartTotal += (item.Count * item.Product.Price); // Price is in ProductDto
                }
                
                _response.Result = cart;

            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpPost("CartUpsert")]
        public async Task<ResponseDto> CartUpsert(CartDto cartDto)
        {
            try
            {
                // Initially we need to find if an entry exists in the CartHeader for that UserId
                var cartHeaderFromDb = await _db.CartHeaders.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == cartDto.CartHeader.UserId);
                if (cartHeaderFromDb == null)
                {
                    // create header and details
                    CartHeader cartHeader = _mapper.Map<CartHeader>(cartDto.CartHeader);
                    // DestinationType destination = _mapper.Map<DestinationType>(sourceObject);
                    _db.CartHeaders.Add(cartHeader);
                    await _db.SaveChangesAsync();
                    // Save changes to retrieve the CartHeaderId.
                    // Associating the Cart Details with the new CartHeaderId.
                    cartDto.CartDetails.First().CartHeaderId = cartHeader.CartHeaderId;
                    _db.CartDetails.Add(_mapper.Map<CartDetails>(cartDto.CartDetails.First()));
                    // Uses First() because only one product can be added at a time.
                    await _db.SaveChangesAsync();
                }
                else
                {
                    // header is not null - there is an entry in CartHeader for that user
                    // then we have to check if CartDetails has the same product -> check based on the productId
                    var cartDetailsFromDb = await _db.CartDetails.AsNoTracking().FirstOrDefaultAsync(
                        u => u.ProductId == cartDto.CartDetails.First().ProductId && 
                        u.CartHeaderId == cartHeaderFromDb.CartHeaderId);
                    // Check the ProductId
                    // Using First() here since CartDetail will have only one entry. Because the only way to add an item to the shopping cart is from the Details page of a product.
                    // So it is impossible that they add two products at the same time.
                    
                    // We need to ensure that the entry is specific to the user we are currently handling.
                    // Because it is possible that a different CartHeaderId might have the same product in their shopping cart. 
                    // We do not want to retrieve that.

                    // This way we find out if the same user has that same product in the shopping cart or not.


                    // If they don't have that, we've to create a new entry - cartDetails.
                    if (cartDetailsFromDb == null)
                    {
                        // create cartDetails (CartHeader already exists and they're adding a new product to the cart)
                        cartDto.CartDetails.First().CartHeaderId = cartHeaderFromDb.CartHeaderId;
                        _db.CartDetails.Add(_mapper.Map<CartDetails>(cartDto.CartDetails.First()));
                        await _db.SaveChangesAsync();
                    }
                    else
                    {   // product is already in the shopping cart
                        // update count in cart details
                        cartDto.CartDetails.First().Count += cartDetailsFromDb.Count;
                        cartDto.CartDetails.First().CartHeaderId = cartDetailsFromDb.CartHeaderId;
                        cartDto.CartDetails.First().CartDetailsId = cartDetailsFromDb.CartDetailsId;
                        _db.CartDetails.Update(_mapper.Map<CartDetails>(cartDto.CartDetails.First()));
                        await _db.SaveChangesAsync();
                    }

                    _response.Result = cartDto;

                    
                }

            }
            catch (Exception ex)
            {
                _response.Message = ex.InnerException?.Message ?? ex.Message; // Get the inner exception details if available
                _response.IsSuccess = false;

                Console.WriteLine($"Error: {ex}"); // Log full exception in console (use logging in production)
            }
            return _response;
            
        }


        [HttpPost("RemoveCart")]
        public async Task<ResponseDto> RemoveCart([FromBody] int cartDetailsId)
        {
            try
            {
                // Retrieve cartDetails from the database 
                CartDetails cartDetails = _db.CartDetails.First(u => u.CartDetailsId == cartDetailsId);

                
                int totalCountofCartItem = _db.CartDetails.Where(u => u.CartHeaderId == cartDetails.CartHeaderId).Count();

                //remove the CartDetails Entry 
                _db.CartDetails.Remove(cartDetails); // removes the item from the cart

                // If that is the only item in the cart for that user, remove the CartHeader as well
                if (totalCountofCartItem == 1)
                {
                    // retrieve the CartHeader from the database
                    var cartHeaderToRemove = await _db.CartHeaders.
                        FirstOrDefaultAsync(u => u.CartHeaderId == cartDetails.CartHeaderId);

                    _db.CartHeaders.Remove(cartHeaderToRemove);
                }
                await _db.SaveChangesAsync();
                _response.Result = true;
            
            }
            catch (Exception ex)
            {
                _response.Message = ex.InnerException?.Message ?? ex.Message; // Get the inner exception details if available
                _response.IsSuccess = false;

                Console.WriteLine($"Error: {ex}"); // Log full exception in console (use logging in production)
            }
            return _response;

        }


    }
}

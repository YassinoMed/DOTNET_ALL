using ApplicationLayer.DTOs;
using ApplicationLayer.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ProductsAPI.Controllers
{
    /// <summary>
    /// Contrôleur CRUD des produits.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>Retourne tous les produits.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ProductDTO>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ProductDTO>>> GetAll()
        {
            var products = await _productService.GetAllProductsAsync();
            return Ok(products);
        }

        /// <summary>Retourne un produit par son identifiant.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ProductDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProductDTO>> GetById(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);

            if (product is null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = $"Product with id {id} was not found.",
                    Instance = HttpContext.Request.Path
                });
            }

            return Ok(product);
        }

        /// <summary>Crée un nouveau produit.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ProductDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProductDTO>> Create([FromBody] CreateProductDTO productDto)
        {
            var created = await _productService.AddProductAsync(productDto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = created!.Id },
                created);
        }

        /// <summary>Met à jour un produit existant.</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDTO productDto)
        {
            if (id != productDto.Id)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = "The route id does not match the product id in the body.",
                    Instance = HttpContext.Request.Path
                });
            }

            await _productService.UpdateProductAsync(productDto);

            return NoContent();
        }

        /// <summary>Supprime un produit.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            await _productService.DeleteProductAsync(id);

            return NoContent();
        }
    }
}

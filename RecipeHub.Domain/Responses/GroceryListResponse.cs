using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecipeHub.Domain.Responses
{
    public class GroceryListResponse
    {
        public Guid UserId { get; set; }
        public List<IngredientResponse> Ingredients { get; set; }
    }
}

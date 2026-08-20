using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecipeHub.Domain.Responses
{
    public class FridgeResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int AmountOfGroceries { get; set; }
        public DateTime Purchased { get; set; }
        public DateTime Retired { get; set; } = DateTime.MinValue;
    }
}

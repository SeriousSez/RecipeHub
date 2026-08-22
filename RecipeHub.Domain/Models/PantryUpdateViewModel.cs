using System;
using System.Collections.Generic;

namespace RecipeHub.Domain.Models
{
    public class PantryUpdateViewModel
    {
        public Guid UserId { get; set; }
        public List<PantryItemViewModel> Items { get; set; } = new List<PantryItemViewModel>();
    }
}
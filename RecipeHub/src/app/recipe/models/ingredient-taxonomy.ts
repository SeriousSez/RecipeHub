export interface IngredientTaxonomyEntry {
    category: string;
    subcategory: string;
    terms: string[];
}

export const INGREDIENT_TAXONOMY: IngredientTaxonomyEntry[] = [
    { category: 'produce', subcategory: 'vegetables.leafyGreens', terms: ['arugula', 'kale', 'lettuce', 'rocket', 'spinach', 'chard'] },
    { category: 'produce', subcategory: 'vegetables.rootVegetables', terms: ['beet', 'carrot', 'parsnip', 'potato', 'radish', 'rutabaga', 'sweet potato', 'turnip', 'yam'] },
    { category: 'produce', subcategory: 'vegetables.cruciferous', terms: ['bok choy', 'broccoli', 'brussels sprout', 'cabbage', 'cauliflower'] },
    { category: 'produce', subcategory: 'vegetables.alliums', terms: ['chive', 'garlic', 'leek', 'onion', 'shallot'] },
    { category: 'produce', subcategory: 'vegetables.nightshades', terms: ['eggplant', 'pepper', 'tomato'] },
    { category: 'produce', subcategory: 'vegetables.squash', terms: ['cucumber', 'pumpkin', 'squash', 'zucchini'] },
    { category: 'produce', subcategory: 'vegetables.other', terms: ['artichoke', 'asparagus', 'avocado', 'bean', 'celery', 'corn', 'green bean', 'mushroom', 'pea'] },
    { category: 'produce', subcategory: 'fruit.berries', terms: ['blackberr', 'blueberr', 'cranberr', 'raspberr', 'strawberr'] },
    { category: 'produce', subcategory: 'fruit.citrus', terms: ['grapefruit', 'lemon', 'lime', 'orange', 'tangerine'] },
    { category: 'produce', subcategory: 'fruit.stoneFruit', terms: ['apricot', 'cherry', 'nectarine', 'peach', 'plum'] },
    { category: 'produce', subcategory: 'fruit.tropical', terms: ['banana', 'coconut', 'kiwi', 'mango', 'papaya', 'passion fruit', 'pineapple'] },
    { category: 'produce', subcategory: 'fruit.other', terms: ['apple', 'fig', 'grape', 'melon', 'pear', 'pomegranate', 'watermelon'] },
    { category: 'produce', subcategory: 'herbs', terms: ['basil', 'cilantro', 'dill', 'mint', 'oregano', 'parsley', 'rosemary', 'sage', 'thyme'] },
    { category: 'produce', subcategory: 'mushrooms', terms: ['champignon', 'mushroom', 'porcini', 'shiitake'] },
    { category: 'meat', subcategory: 'poultry', terms: ['chicken', 'duck', 'turkey'] },
    { category: 'meat', subcategory: 'beef', terms: ['beef', 'steak', 'veal'] },
    { category: 'meat', subcategory: 'pork', terms: ['bacon', 'ham', 'pork', 'sausage'] },
    { category: 'meat', subcategory: 'lamb', terms: ['lamb', 'mutton'] },
    { category: 'seafood', subcategory: 'fish', terms: ['cod', 'herring', 'salmon', 'sardine', 'trout', 'tuna'] },
    { category: 'seafood', subcategory: 'shellfish', terms: ['anchovy', 'clam', 'crab', 'mussel', 'oyster', 'prawn', 'scallop', 'shrimp'] },
    { category: 'dairy', subcategory: 'milkAndCream', terms: ['buttermilk', 'cream', 'milk'] },
    { category: 'dairy', subcategory: 'cheese', terms: ['cheddar', 'cheese', 'mozzarella', 'parmesan', 'ricotta'] },
    { category: 'dairy', subcategory: 'eggs', terms: ['egg'] },
    { category: 'grains', subcategory: 'riceAndPasta', terms: ['couscous', 'noodle', 'pasta', 'quinoa', 'rice'] },
    { category: 'grains', subcategory: 'breadAndBakery', terms: ['bagel', 'bread', 'bun', 'cake', 'croissant', 'pastry', 'tortilla'] },
    { category: 'grains', subcategory: 'flourAndCereals', terms: ['cereal', 'flour', 'oat', 'porridge'] },
    { category: 'legumes', subcategory: 'beansAndPeas', terms: ['chickpea', 'lentil', 'soy', 'split pea'] },
    { category: 'nutsAndSeeds', subcategory: 'nuts', terms: ['almond', 'cashew', 'hazelnut', 'peanut', 'pecan', 'pistachio', 'walnut'] },
    { category: 'nutsAndSeeds', subcategory: 'seeds', terms: ['chia', 'flax', 'pumpkin seed', 'sesame', 'sunflower seed'] },
    { category: 'herbsAndSpices', subcategory: 'herbs', terms: ['bay leaf', 'chervil', 'tarragon'] },
    { category: 'herbsAndSpices', subcategory: 'spices', terms: ['cinnamon', 'cumin', 'curry', 'italian seasoning', 'nutmeg', 'paprika', 'pepper', 'salt', 'turmeric'] },
    { category: 'oilsAndFats', subcategory: 'oils', terms: ['oil', 'olive oil', 'sesame oil'] },
    { category: 'baking', subcategory: 'bakingBasics', terms: ['baking powder', 'baking soda', 'cocoa', 'sugar', 'vanilla', 'yeast'] },
    { category: 'condiments', subcategory: 'sauces', terms: ['broth', 'ketchup', 'mayonnaise', 'mustard', 'sauce', 'stock', 'tomato paste'] },
    { category: 'beverages', subcategory: 'drinks', terms: ['coffee', 'juice', 'tea', 'water', 'wine'] }
];

export function classifyIngredient(name: string): IngredientTaxonomyEntry {
    const normalizedName = (name ?? '').trim().toLocaleLowerCase();
    return INGREDIENT_TAXONOMY.find(entry => entry.terms.some(term => normalizedName.includes(term))) ?? {
        category: 'other',
        subcategory: 'other',
        terms: []
    };
}

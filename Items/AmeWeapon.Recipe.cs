using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Ame.Items
{
	/// <summary>
	/// Receta para crear el Arma Ame
	/// </summary>
	public partial class AmeWeapon : ModItem
	{
		public override void AddRecipes()
		{
			// Receta Post-Moon Lord (muy poderosa)
			Recipe recipe = CreateRecipe();
			
			// Fragmentos de los pilares
			recipe.AddIngredient(ItemID.FragmentSolar, 10);
			recipe.AddIngredient(ItemID.FragmentVortex, 10);
			recipe.AddIngredient(ItemID.FragmentNebula, 10);
			recipe.AddIngredient(ItemID.FragmentStardust, 10);
			
			// Materiales legendarios
			recipe.AddIngredient(ItemID.LunarBar, 15);
			
			// Estación de crafteo
			recipe.AddTile(TileID.LunarCraftingStation);
			
			recipe.Register();
			
			// Receta alternativa más accesible para testing
			Recipe testRecipe = CreateRecipe();
			testRecipe.AddIngredient(ItemID.DirtBlock, 10);
			testRecipe.AddTile(TileID.WorkBenches);
			// Descomentar la siguiente línea solo para testing:
			// testRecipe.Register();
		}
	}
}

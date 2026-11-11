// SPDX-FileCopyrightText: 2025 AlgisAlphonse
// SPDX-FileCopyrightText: 2025 Ilya246
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Construction;
using Content.Client.Construction.UI;
using Content.Shared._Goobstation.Factory;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Whitelist;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Goobstation.Client.Factory.UI;

public sealed class ConstructorBUI : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private readonly ConstructionSystem _construction;
    private readonly EntityWhitelistSystem _whitelist;
    private readonly SpriteSystem _sprite;

    private ConstructionMenu? _menu;
    private string? _id;
    private readonly LocId _favoriteCatName = "construction-category-favorites";
    private readonly LocId _forAllCategoryName = "construction-category-all";

    public ConstructorBUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _construction = EntMan.System<ConstructionSystem>();
        _whitelist = EntMan.System<EntityWhitelistSystem>();
        _sprite = EntMan.System<SpriteSystem>();

        _id = EntMan.GetComponentOrNull<ConstructorComponent>(owner)?.Construction;
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ConstructionMenu>();
        PopulateCategories();
        PopulateRecipes(string.Empty, string.Empty);
        _menu.PopulateRecipes += (_, args) => PopulateRecipes(args.Item1, args.Item2);
        _menu.RecipeSelected += (_, item) =>
        {
            _menu.ClearRecipeInfo();
            if (item is null)
            {
                _id = null;
                return;
            }

            var proto = item.Prototype;
            _id = proto.ID;
            _menu.SetRecipeInfo(proto.Name ?? string.Empty, proto.Description ?? string.Empty,
                item.TargetPrototype, proto.Type != ConstructionType.Item, true);

            GenerateStepList(proto);
        };
        _menu.BuildButtonToggled += (_, _) =>
        {
            SendPredictedMessage(new ConstructorSetProtoMessage(_id));
            _menu.Close();
        };
    }

    private void PopulateCategories(string? selected = null)
    {
        if (_menu is not {} menu)
            return;

        var categories = new HashSet<string>();

        foreach (var prototype in _proto.EnumeratePrototypes<ConstructionPrototype>())
        {
            var category = prototype.Category;

            if (!string.IsNullOrEmpty(category))
                categories.Add(category);
        }

        var categoriesArray = new string[categories.Count + 1];

        // hard-coded to show all recipes
        var idx = 0;
        categoriesArray[idx++] = _forAllCategoryName;

        foreach (var cat in categories.OrderBy(Loc.GetString))
        {
            categoriesArray[idx++] = cat;
        }

        menu.OptionCategories.Clear();

        for (var i = 0; i < categoriesArray.Length; i++)
        {
            menu.OptionCategories.AddItem(Loc.GetString(categoriesArray[i]), i);

            if (!string.IsNullOrEmpty(selected) && selected == categoriesArray[i])
                menu.OptionCategories.SelectId(i);
        }

        menu.Categories = categoriesArray;
    }

    // copypasted and optimised from ConstructionMenuPresenter
    private void PopulateRecipes(string search, string category)
    {
        if (PlayerManager.LocalEntity is not { } user || _menu is not { } menu)
            return;

        search = search.Trim();
        var isEmptyCategory = string.IsNullOrEmpty(category) || category == _forAllCategoryName;
        var recipes = new List<ConstructionMenu.ConstructionMenuListData>();

        foreach (var recipe in _proto.EnumeratePrototypes<ConstructionPrototype>())
        {
            if (recipe.Hide || _whitelist.IsWhitelistFail(recipe.EntityWhitelist, user))
                continue;

            if (search.Length > 0 &&
                !recipe.Name!.Contains(search, StringComparison.InvariantCultureIgnoreCase))
                continue;

            if (!isEmptyCategory && (category == _favoriteCatName || recipe.Category != category))
                continue;

            if (!_construction.TryGetRecipePrototype(recipe.ID, out var targetProtoId))
                continue;

            if (!_proto.TryIndex(targetProtoId, out EntityPrototype? targetProto))
                continue;

            recipes.Add(new(recipe, targetProto));
        }

        recipes.Sort((a, b) => string.Compare(a.Prototype.Name, b.Prototype.Name,
            StringComparison.InvariantCulture));

        menu.RecipesGridScrollContainer.Visible = false;
        menu.Recipes.Visible = true;
        menu.Recipes.PopulateList(recipes);
    }

    private void GenerateStepList(ConstructionPrototype proto)
    {
        if (_construction.GetGuide(proto) is not { } guide
            || _menu is not { } menu)
            return;

        var list = menu.RecipeStepList;
        foreach (var entry in guide.Entries)
        {
            var text = entry.Arguments != null
                ? Loc.GetString(entry.Localization, entry.Arguments)
                : Loc.GetString(entry.Localization);

            if (entry.EntryNumber is { } number)
                text = Loc.GetString("construction-presenter-step-wrapper",
                    ("step-number", number), ("text", text));

            // The padding needs to be applied regardless of text length... (See PadLeft documentation)
            text = text.PadLeft(text.Length + entry.Padding);

            var icon = entry.Icon != null ? _sprite.Frame0(entry.Icon) : Texture.Transparent;
            list.AddItem(text, icon, false);
        }
    }
}

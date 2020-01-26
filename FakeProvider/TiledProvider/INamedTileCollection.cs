﻿#region Using
using OTAPI.Tile;
using System;
using System.Collections.Generic;
using Terraria;
#endregion
namespace FakeProvider
{
    public interface INamedTileCollection : ITileCollection, IDisposable
    {
        IProviderTile this[int x, int y] { get; set; }

        TileProviderCollection ProviderCollection { get; }
        string Name { get; }
        int X { get; set; }
        int Y { get; set; }
        int Layer { get; }
        bool Enabled { get; }

        (int X, int Y, int Width, int Height) XYWH();
        void SetXYWH(int X, int Y, int Width, int Height);
        void Move(int X, int Y);
        void Draw(bool section);
        void Enable();
        void Disable();
        void HideSignsChestsEntities();
        void UpdateSignsChestsEntities();

        FakeSign AddSign(int X, int Y, string Text);
        void RemoveSign(FakeSign Sign);
        void UpdateSigns();

        FakeChest AddChest(int X, int Y, Item[] Items = null);
        void RemoveChest(FakeChest Sign);
        void UpdateChests();
    }
}
﻿#region Using
using OTAPI.Tile;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Terraria;
#endregion
namespace FakeProvider
{
    public class TileProviderCollection : ITileCollection, IDisposable, IEnumerable<INamedTileCollection>
    {
        #region Data

        // ImmutableList?????????????????????????????????????????????????????????????????????????????????????
        // ??????????????????????????????????????????????????????????????????????????????????????????????????
        // ????????????????????????????????????????????????
        internal List<INamedTileCollection> Providers = new List<INamedTileCollection>();

        internal IProviderTile[,] Tiles;
        /// <summary> World width visible by client. </summary>
        public int Width { get; }
        /// <summary> World height visible by client. </summary>
        public int Height { get; }
        /// <summary> Horizontal offset of the loaded world. </summary>
        public int OffsetX { get; }
        /// <summary> Vertical offset of the loaded world. </summary>
        public int OffsetY { get; }
        /// <summary> Tile to be visible outside of all providers. </summary>
        public ReadonlyFakeTile VoidTile { get; }
        private object Locker { get; } = new object();

        #endregion
        #region Constructor

        public TileProviderCollection(int Width, int Height,
            int OffsetX, int OffsetY, ReadonlyFakeTile VoidTile = null)
        {
            this.Width = Width;
            this.Height = Height;
            this.OffsetX = OffsetX;
            this.OffsetY = OffsetY;
            this.VoidTile = VoidTile ?? new ReadonlyFakeTile(-1);

            Tiles = new IProviderTile[this.Width, this.Height];
        }

        #endregion

        #region operator[,]

        public ITile this[int X, int Y]
        {
            get
            {
                return Tiles[X, Y] ?? (ITile)VoidTile;
            }
            set
            {
                Tiles[X, Y].CopyFrom(value);
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            lock (Locker)
            {
                foreach (INamedTileCollection provider in Providers)
                    (provider as IDisposable).Dispose();
            }
        }

        #endregion
        
        #region operator[]

        public INamedTileCollection this[string Name] =>
            Providers.FirstOrDefault(p => (p.Name == Name));

        #endregion

        #region Add

        public void Add(INamedTileCollection Provider)
        {
            lock (Locker)
            {
                if (Providers.Any(p => (p.Name == Provider.Name)))
                    throw new ArgumentException($"Tile collection '{Provider.Name}' " +
                        "is already in use. Name must be unique.");
                short index = (short)Providers.FindIndex(p => (p.Layer > Provider.Layer));
                if (index == -1)
                    index = (short)Providers.Count;
                Provider.SetupParent(this, index);
                Providers.Insert(index, Provider);
                Provider.Apply();
                Provider.Draw(true);
            }
        }

        #endregion
        #region Remove

        public bool Remove(string Name, bool Cleanup = true)
        {
            lock (Locker)
            {
                INamedTileCollection provider = Providers.FirstOrDefault(p => (p.Name == Name));
                if (provider == null)
                    return false;
                Providers.Remove(provider);

                UpdateTiles(provider.X, provider.Y, provider.Width, provider.Height);
                provider.Draw(true);

                provider.Dispose();
                if (Cleanup)
                    GC.Collect();
                return true;
            }
        }

        #endregion
        #region Clear

        public void Clear(INamedTileCollection except = null)
        {
            lock (Locker)
            {
                foreach (INamedTileCollection provider in Providers.ToArray())
                    if (provider != except)
                        Remove(provider.Name, false);
                GC.Collect();
            }
        }

        #endregion

        #region Intersect

        internal static void Intersect(INamedTileCollection First, INamedTileCollection Second,
            out int RX, out int RY, out int RWidth, out int RHeight)
        {
            int ex1 = Second.X + Second.Width;
            int ex2 = First.X + First.Width;
            int ey1 = Second.Y + Second.Height;
            int ey2 = First.Y + First.Height;
            int maxSX = (Second.X > First.X) ? Second.X : First.X;
            int maxSY = (Second.Y > First.Y) ? Second.Y : First.Y;
            int minEX = (ex1 < ex2) ? ex1 : ex2;
            int minEY = (ey1 < ey2) ? ey1 : ey2;
            RX = maxSX;
            RY = maxSY;
            RWidth = minEX - maxSX;
            RHeight = minEY - maxSY;
        }

        internal static void Intersect(INamedTileCollection Provider, int X, int Y, int Width, int Height,
            out int RX, out int RY, out int RWidth, out int RHeight)
        {
            int ex1 = Provider.X + Provider.Width;
            int ex2 = X + Width;
            int ey1 = Provider.Y + Provider.Height;
            int ey2 = Y + Height;
            int maxSX = (Provider.X > X) ? Provider.X : X;
            int maxSY = (Provider.Y > Y) ? Provider.Y : Y;
            int minEX = (ex1 < ex2) ? ex1 : ex2;
            int minEY = (ey1 < ey2) ? ey1 : ey2;
            RX = maxSX;
            RY = maxSY;
            RWidth = minEX - maxSX;
            RHeight = minEY - maxSY;
        }

        #endregion
        #region IsIntersecting

        internal static bool IsIntersecting(INamedTileCollection First, INamedTileCollection Second) =>
            ((First.X < (Second.X + Second.Width)) && (Second.X < (First.X + First.Width))
            && (First.Y < (Second.Y + Second.Height)) && (Second.Y < (First.Y + First.Height)));

        internal static bool IsIntersecting(INamedTileCollection Provider, int X, int Y, int Width, int Height) =>
            ((Provider.X < (X + Width)) && (X < (Provider.X + Provider.Width))
            && (Provider.Y < (Y + Height)) && (Y < (Provider.Y + Provider.Height)));

        #endregion
        #region UpdateTiles

        /// <summary>
        /// Update tiles depending on fake providers. Relative to offset (world position).
        /// </summary>
        public void UpdateTiles(int X, int Y, int Width, int Height)
        {
            lock (Locker)
                for (short providerIndex = 0; providerIndex < Providers.Count; providerIndex++)
                {
                    INamedTileCollection provider = Providers[providerIndex];
                    if (provider.Enabled && IsIntersecting(provider, X, Y, Width, Height))
                    {
                        Intersect(provider, X, Y, Width, Height, out int x, out int y, out int w, out int h);
                        int providerX = provider.X;
                        int providerY = provider.Y;
                        for (int i = x; i < x + w; i++)
                            for (int j = y; j < y + h; j++)
                                Tiles[i + OffsetX, j + OffsetY] =
                                    (IProviderTile)provider[i - providerX, j - providerY];
                    }
                }
        }

        #endregion
        
        #region SetTop

        public void SetTop(string Name)
        {
            lock (Locker)
            {
                INamedTileCollection provider = Providers.FirstOrDefault(p => (p.Name == Name));
                if (provider == null)
                    return;
                Remove(provider.Name);
                Add(provider);
            }
        }

        #endregion

        #region GetEnumerator

        public IEnumerator<INamedTileCollection> GetEnumerator() =>
            Providers.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            Providers.GetEnumerator();

        #endregion
    }
}
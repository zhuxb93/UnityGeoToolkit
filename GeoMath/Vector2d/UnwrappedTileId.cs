using System;

namespace GeoToolkit
{

    /// <summary>
    ///     Unwrapped tile identifier in a slippy map. Similar to <see cref="CanonicalTileId"/>,
    ///     but might go around the globe.
    /// </summary>
    public struct UnwrappedTileId : IEquatable<UnwrappedTileId>
    {
        /// <summary> The zoom level. </summary>
        public readonly int Z;

        /// <summary> The X coordinate in the tile grid. </summary>
        public readonly int X;

        /// <summary> The Y coordinate in the tile grid. </summary>
        public readonly int Y;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UnwrappedTileId"/> struct,
        ///     representing a tile coordinate in a slippy map that might go around the
        ///     globe.
        /// </summary>
        /// <param name="z">The z coordinate.</param>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public UnwrappedTileId(int z, int x, int y)
        {
            this.Z = z;
            this.X = x;
            this.Y = y;
        }

        /// <summary> Gets the canonical tile identifier. </summary>
        /// <value> The canonical tile identifier. </value>
        public CanonicalTileId Canonical
        {
            get
            {
                return new CanonicalTileId(this);
            }
        }

        /// <summary>
        ///     Returns a <see cref="T:System.String"/> that represents the current
        ///     <see cref="T:Mapbox.Map.UnwrappedTileId"/>.
        /// </summary>
        /// <returns>
        ///     A <see cref="T:System.String"/> that represents the current
        ///     <see cref="T:Mapbox.Map.UnwrappedTileId"/>.
        /// </returns>
        public override string ToString()
        {
            return this.Z + "-" + this.X + "-" + this.Y;
        }

        public bool Equals(UnwrappedTileId other)
        {
            return this.X == other.X && this.Y == other.Y && this.Z == other.Z;
        }

        public override int GetHashCode()
        {
            return (X << 6) ^ (Y << 16) ^ (Z << 8);
        }

        public override bool Equals(object obj)
        {
            return this.X == ((UnwrappedTileId)obj).X && this.Y == ((UnwrappedTileId)obj).Y && this.Z == ((UnwrappedTileId)obj).Z;
        }

        public static bool operator ==(UnwrappedTileId a, UnwrappedTileId b)
        {
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        public static bool operator !=(UnwrappedTileId a, UnwrappedTileId b)
        {
            return !(a == b);
        }

        public UnwrappedTileId North
        {
            get
            {
                return new UnwrappedTileId(Z, X, Y - 1);
            }
        }

        public UnwrappedTileId East
        {
            get
            {
                return new UnwrappedTileId(Z, X + 1, Y);
            }
        }

        public UnwrappedTileId South
        {
            get
            {
                return new UnwrappedTileId(Z, X, Y + 1);
            }
        }

        public UnwrappedTileId West
        {
            get
            {
                return new UnwrappedTileId(Z, X - 1, Y);
            }
        }

        public UnwrappedTileId NorthEast
        {
            get
            {
                return new UnwrappedTileId(Z, X + 1, Y - 1);
            }
        }

        public UnwrappedTileId SouthEast
        {
            get
            {
                return new UnwrappedTileId(Z, X + 1, Y + 1);
            }
        }

        public UnwrappedTileId NorthWest
        {
            get
            {
                return new UnwrappedTileId(Z, X - 1, Y - 1);
            }
        }

        public UnwrappedTileId SouthWest
        {
            get
            {
                return new UnwrappedTileId(Z, X - 1, Y + 1);
            }
        }
    }


    public struct CanonicalTileId : IEquatable<CanonicalTileId>
    {
        /// <summary> The zoom level. </summary>
        public readonly int Z;

        /// <summary> The X coordinate in the tile grid. </summary>
        public readonly int X;

        /// <summary> The Y coordinate in the tile grid. </summary>
        public readonly int Y;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CanonicalTileId"/> struct,
        ///     representing a tile coordinate in a slippy map.
        /// </summary>
        /// <param name="z"> The z coordinate or the zoom level. </param>
        /// <param name="x"> The x coordinate. </param>
        /// <param name="y"> The y coordinate. </param>
        public CanonicalTileId(int z, int x, int y)
        {
            this.Z = z;
            this.X = x;
            this.Y = y;
        }

        internal CanonicalTileId(UnwrappedTileId unwrapped)
        {
            var z = unwrapped.Z;
            var x = unwrapped.X;
            var y = unwrapped.Y;

            var wrap = (x < 0 ? x - (1 << z) + 1 : x) / (1 << z);

            this.Z = z;
            this.X = x - wrap * (1 << z);
            this.Y = y < 0 ? 0 : Math.Min(y, (1 << z) - 1);
        }

        /// <summary>
        ///     Get the cordinate at the top left of corner of the tile.
        /// </summary>
        /// <returns> The coordinate. </returns>
        public Vector2d ToVector2d()
        {
            double n = Math.PI - ((2.0 * Math.PI * this.Y) / Math.Pow(2.0, this.Z));

            double lat = 180.0 / Math.PI * Math.Atan(Math.Sinh(n));
            double lng = (this.X / Math.Pow(2.0, this.Z) * 360.0) - 180.0;

            // FIXME: Super hack because of rounding issues.
            return new Vector2d(lat - 0.0001, lng + 0.0001);
        }

        /// <summary>
        ///     Returns a <see cref="T:System.String"/> that represents the current
        ///     <see cref="T:Mapbox.Map.CanonicalTileId"/>.
        /// </summary>
        /// <returns>
        ///     A <see cref="T:System.String"/> that represents the current
        ///     <see cref="T:Mapbox.Map.CanonicalTileId"/>.
        /// </returns>
        public override string ToString()
        {
            return this.Z + "/" + this.X + "/" + this.Y;
        }

        #region Equality 
        public bool Equals(CanonicalTileId other)
        {
            return this.X == other.X && this.Y == other.Y && this.Z == other.Z;
        }

        public override int GetHashCode()
        {
            return X ^ Y ^ Z;
        }

        public static bool operator ==(CanonicalTileId a, CanonicalTileId b)
        {
            return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        }

        public static bool operator !=(CanonicalTileId a, CanonicalTileId b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj is CanonicalTileId)
            {
                return this.Equals((CanonicalTileId)obj);
            }
            else
            {
                return false;
            }
        }

        #endregion
    }
}
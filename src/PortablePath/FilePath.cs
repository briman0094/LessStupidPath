using System.Collections.Generic;
using System.Linq;

namespace System.IO
{
	/// <summary>
	/// File path object. Converts between paths and strings, and performs path manipulation
	/// </summary>
	public class FilePath : IEquatable<FilePath>
	{
		private readonly List<string> parts;
		private readonly bool rooted;
		private readonly bool empty;

		/// <summary> Create a file path from a path string </summary>
		public FilePath( string input )
		{
			this.parts = new List<string>(
				input.Split( new[] { '/', '\\', ':' }, StringSplitOptions.RemoveEmptyEntries )
			);
			this.rooted = FilePath.IsRooted( input );
			this.empty = string.IsNullOrWhiteSpace( input );
		}

		/// <summary> Append 'right' to this path, ignoring navigation semantics </summary>
		public FilePath Append( FilePath right )
		{
			return this.empty ? right : new FilePath( this.parts.Concat( right.parts ), this.rooted );
		}

		/// <summary> Append 'right' to this path, obeying standard navigation semantics </summary>
		public FilePath Navigate( FilePath navigation )
		{
			if ( navigation.rooted ) return navigation.Normalise();
			return new FilePath( this.parts.Concat( navigation.parts ), this.rooted ).Normalise();
		}

		/// <summary> Remove a common root from this path and return a relative path </summary>
		public FilePath Unroot( FilePath root )
		{
			for ( int i = 0; i < root.parts.Count; i++ )
			{
				if ( this.parts.Count <= i ) throw new InvalidOperationException( "Root supplied is longer than full path" );
				if ( this.parts[ i ] != root.parts[ i ] ) throw new InvalidOperationException( "Full path is not a subpath of root" );
			}
			return new FilePath( this.parts.Skip( root.parts.Count ), false );
		}

		/// <summary> Returns a minimal relative path from source to current path </summary>
		public FilePath RelativeTo( FilePath source )
		{
			if ( this.parts.Count == 1 && source.parts.Count == 1 ) return this;
			int shorter = Math.Min( this.parts.Count, source.parts.Count );
			int common;
			for ( common = 0; common < shorter; common++ )
				if ( this.parts[ common ] != source.parts[ common ] ) break;

			if ( common == 0 )
			{
				if ( this.rooted ) return this;
				return source.Navigate( this );
			}

			int differences = source.parts.Count - common;
			if ( differences == 0 ) return new FilePath( this.parts.Skip( common ), false ); // same path

			List<string> result = new List<string>();
			for ( int i = 1; i < differences; i++ )
				result.Add( ".." );

			result.AddRange( this.parts.Skip( common ) );

			return new FilePath( result, false );
		}

		/// <summary> Remove single dots, remove path elements for double dots. </summary>
		public FilePath Normalise()
		{
			List<string> result = new List<string>();
			uint leading = 0;

			for ( int index = this.parts.Count - 1; index >= 0; index-- )
			{
				string part = this.parts[ index ];
				switch ( part )
				{
					case ".":
						continue;
					case "..":
						leading++;
						continue;
				}

				if ( leading > 0 )
				{
					leading--;
					continue;
				}
				result.Insert( 0, part );
			}

			if ( this.rooted && leading > 0 ) throw new InvalidOperationException( "Tried to navigate before path root" );

			for ( int i = 0; i < leading; i++ )
			{
				result.Insert( 0, ".." );
			}

			return new FilePath( result, this.rooted );
		}

		public FilePath EnsureExtension( string extension )
		{
			string result = this.ToString();

			if ( !this.HasExtension() || this.Extension().ToLower() != extension.ToLower() )
				result += $".{extension}";

			return new FilePath( result );
		}

		/// <summary>
		/// Returns true if the path specified was empty, false otherwise
		/// </summary>
		public bool IsEmpty()
		{
			return this.empty;
		}

		/// <summary> Returns a string representation of the path using Posix path separators </summary>
		public string ToPosixPath()
		{
			return this.rooted
				? "/" + string.Join( "/", this.parts )
				: string.Join( "/", this.parts );
		}

		/// <summary> Returns a string representation of the path using Windows path separators </summary>
		public string ToWindowsPath()
		{
			return ( this.rooted )
				? this.RootedWindowsPath()
				: string.Join( "\\", this.Normalise().parts );
		}

		/// <summary>
		/// Returns a string representation of the directories of the path using separators for the current execution environment
		/// </summary>
		public string DirectoryName( bool? forcePosix = null )
		{
			string path = this.ToEnvironmentalPath( forcePosix );
			return path.Substring( 0, path.Length - this.FileNameWithExtension().Length - 1 );
		}

		/// <summary> Returns a string representation of the path using path separators for the current execution environment </summary>
		public string ToEnvironmentalPath( bool? forcePosix = null )
		{
			return ( forcePosix ?? FilePath.PosixOS() ) ? this.ToPosixPath() : this.ToWindowsPath();
		}

		/// <summary>
		/// Returns true if the path string has a root element (such as `/` or `C:\`)
		/// <para>Return false if the path string is relative</para>
		/// </summary>
		public static bool IsRooted( string input )
		{
			return ( input.Length >= 1
					&& ( input[ 0 ] == '/' || input[ 0 ] == '\\' ) )
					|| ( input.Length >= 2 && input[ 1 ] == ':' );
		}

		public bool HasExtension()
		{
			return !FilePath.DirectorySeperatorAfterDot( this.ToEnvironmentalPath() );
		}

		/// <summary>
		/// Returns the extension of a file, not including the `.`
		/// <para>Throws InvalidOperationException if the file has no extension</para>
		/// </summary>
		public string Extension()
		{
			if ( !this.HasExtension() )
				return null;

			// ReSharper disable StringLastIndexOfIsCultureSpecific.1
			string fullPath = this.ToEnvironmentalPath();
			return fullPath.Substring( fullPath.LastIndexOf( "." ) + 1 ).ToLower();
			// ReSharper restore StringLastIndexOfIsCultureSpecific.1
		}

		/// <summary>
		/// Returns the last element of the path (either directory or file)
		/// </summary>
		public string LastElement()
		{
			return this.parts.LastOrDefault();
		}

		/// <summary>
		/// Returns the last element of the path, with the last extension removed
		/// <para>(i.e. `../dir/file.exe.old` becomes `file.exe`; `myfile.txt` becomes `myfile`)</para>
		/// </summary>
		public string FileNameWithoutExtension()
		{
			string fileNameWithExtension = this.parts.Last();
			if ( !this.HasExtension() )
				return fileNameWithExtension;

			int lastIndexOf = fileNameWithExtension.LastIndexOf( this.Extension(), StringComparison.Ordinal );
			return fileNameWithExtension.Substring( 0, lastIndexOf - 1 );
		}

		/// <summary>
		/// Returns the last element of the path, with extension.
		/// <para>Throws InvalidOperationException if the file has no extension</para>
		/// </summary>
		public string FileNameWithExtension()
		{
			return this.HasExtension() ? this.FileNameWithoutExtension() + "." + this.Extension() : this.FileNameWithoutExtension();
		}

		#region Private Helper Functions

		private string WindowsDriveSpecOrFolder()
		{
			if ( this.parts.Count < 1 ) return "";

			if ( this.parts[ 0 ].Length == 1 ) return this.parts[ 0 ] + ":";
			return "\\" + this.parts.First();
		}

		private string RootedWindowsPath()
		{
			if ( this.parts.Count < 2 ) return "\\" + this.WindowsDriveSpecOrFolder();

			return string.Join( "\\", this.WindowsDriveSpecOrFolder(), string.Join( "\\", this.parts.Skip( 1 ) ) );
		}

		/// <summary>
		/// Returns whether or not this is a POSIX-path-based OS.
		/// 
		/// PCLs do not have access to specific platform details, but it is possible to infer the path separator type
		/// based on the newline string. If the newline string contains "\r", this is a Windows-based system,
		/// and it should use backslashes. Otherwise, it should use forward slashes.
		/// </summary>
		private static bool PosixOS()
		{
			return !Environment.NewLine.Contains( "\r" );
		}

		public static char DirectorySeparator => FilePath.PosixOS() ? '/' : '\\';
		public static char PathSeparator => FilePath.PosixOS() ? ':' : ';';

		/// <summary>
		/// Build a file path from elements and a root flag
		/// </summary>
		protected FilePath( IEnumerable<string> orderedElements, bool rooted )
		{
			this.parts = orderedElements.ToList();
			this.rooted = rooted;
		}

		private static bool DirectorySeperatorAfterDot( string fullPath )
		{
// ReSharper disable StringLastIndexOfIsCultureSpecific.1
			return fullPath.LastIndexOf( "\\" ) > fullPath.LastIndexOf( "." ) ||
					fullPath.LastIndexOf( "/" ) > fullPath.LastIndexOf( "." );
// ReSharper restore StringLastIndexOfIsCultureSpecific.1
		}

		#endregion

		#region Operators, equality and other such fluff

		/// <summary>
		/// Convert a string path to a `FilePath`
		/// </summary>
		public static explicit operator FilePath( string src )
		{
			return new FilePath( src );
		}

		/// <summary>
		/// Equality
		/// </summary>
		public bool Equals( FilePath other )
		{
			if ( object.ReferenceEquals( null, other ) ) return false;
			if ( object.ReferenceEquals( this, other ) ) return true;
			return this.Normalise().ToPosixPath() == other.Normalise().ToPosixPath();
		}

		/// <summary>
		/// Returns a posix path version of the FilePath
		/// </summary>
		public override string ToString()
		{
			return this.ToEnvironmentalPath();
		}

		/// <summary>
		/// Equality
		/// </summary>
		public override bool Equals( object obj )
		{
			if ( object.ReferenceEquals( null, obj ) ) return false;
			if ( object.ReferenceEquals( this, obj ) ) return true;
			if ( obj.GetType() != this.GetType() ) return false;
			return this.Equals( (FilePath) obj );
		}

		/// <summary>
		/// Equality
		/// </summary>
		public override int GetHashCode()
		{
			unchecked
			{
				return ( ( this.parts?.GetHashCode() ?? 0 )*397 ) ^ this.rooted.GetHashCode();
			}
		}

		/// <summary>
		/// Equality
		/// </summary>
		public static bool operator ==( FilePath a, FilePath b )
		{
			if ( object.ReferenceEquals( a, b ) ) return true;
			if ( ( (object) a == null ) || ( (object) b == null ) ) return false;
			return a.Normalise().ToPosixPath() == b.Normalise().ToPosixPath();
		}

		/// <summary>
		/// Inequality
		/// </summary>
		public static bool operator !=( FilePath a, FilePath b )
		{
			return !( a == b );
		}

		#endregion
	}
}
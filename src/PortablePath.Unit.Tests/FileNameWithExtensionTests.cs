﻿using System.IO;
using NUnit.Framework;

namespace LessStupidPath.Unit.Tests
{
	[ TestFixture ]
	public class FileNameWithExtensionTests
	{
		[ Test ]
		[ TestCase( "/whatever/this/is.la" ) ]
		[ TestCase( "/is.la" ) ]
		[ TestCase( "hello/is.la" ) ]
		[ TestCase( "is.la" ) ]
		[ TestCase( @"c:\is.la" ) ]
		[ TestCase( @"c:\hello\is.la" ) ]
		[ TestCase( @"is.la" ) ]
		[ TestCase( @"a\d\is.la" ) ]
		[ TestCase( @"\is.la" ) ]
		public void Should_get_filename( string path )
		{
			FilePath filePath = new FilePath( path );

			Assert.That( filePath.FileNameWithExtension(), Is.EqualTo( "is.la" ) );
		}
	}
}
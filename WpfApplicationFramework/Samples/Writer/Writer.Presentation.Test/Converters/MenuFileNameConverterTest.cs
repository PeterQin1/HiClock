﻿using System;
using System.Waf.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Waf.Writer.Presentation.Converters;

namespace Waf.Writer.Presentation.Test.Converters
{
    [TestClass]
    public class MenuFileNameConverterTest
    {
        [TestMethod]
        public void Convert()
        {
            MenuFileNameConverter converter = MenuFileNameConverter.Default;

            Assert.AreEqual("", converter.Convert(null, null, null, null));
            Assert.AreEqual("Document 1.rtf", converter.Convert(@"C:\Document 1.rtf", null, null, null));
            Assert.AreEqual("This is a document with a very long f...", converter.Convert(@"C:\This is a document with a very long file name.rtf", null, null, null));

            AssertHelper.ExpectedException<NotSupportedException>(() => converter.ConvertBack(null, null, null, null));
        }
    }
}

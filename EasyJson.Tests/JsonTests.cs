using Microsoft.VisualStudio.TestTools.UnitTesting;
using EasyJson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace EasyJson.Tests
{

    [TestClass()]
    public class JsonTests
    {
        private FileStream fs;
        [TestInitialize]
        public void Initialize()
        {
            fs = new FileStream("Data/data2.json", FileMode.Open);
        }

        [TestMethod()]
        public void ParseTest()
        {
            var strJson = "{\"name\":\"jello\"}";
            PrivateType privateType = new PrivateType(typeof(Json));
            var jsonItem = privateType.InvokeStatic("Parse", strJson) as JsonItem;
            Assert.AreEqual("jello", jsonItem["name"].Value);
        }

        [TestMethod()]
        public void ParseFromFileTest()
        {
            var jsonPath = "Data/data1.json";
            PrivateType privateType = new PrivateType(typeof(Json));
            var jsonItem = privateType.InvokeStatic("ParseFromFile", jsonPath) as JsonItem;
            Assert.AreEqual("jello chen", jsonItem["name"].Value);
        }

        [TestMethod()]
        public void ParseFromStreamTest()
        {
            PrivateType privateType = new PrivateType(typeof(Json));
            var jsonItem = privateType.InvokeStatic("ParseFromStream", fs) as JsonItem;
            Assert.AreEqual("jello chen", jsonItem["name"].Value);
        }

        [TestMethod]
        public void ParseImplTest()
        {
            var strJson = "{\"name\":\"jello\"}";
            var reader = new StringReader(strJson);
            PrivateType privateType = new PrivateType(typeof(Json));
            var jsonItem = privateType.InvokeStatic("ParseImpl", reader) as JsonItem;
            Assert.AreEqual("jello", jsonItem["name"].Value);
        }

        [TestCleanup]
        public void UnInitialize()
        {
            fs.Dispose();
        }
    }
}
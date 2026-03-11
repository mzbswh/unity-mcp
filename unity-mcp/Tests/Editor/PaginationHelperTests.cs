using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Tests.Editor
{
    public class PaginationHelperTests
    {
        private List<int> _items;

        [SetUp]
        public void SetUp()
        {
            _items = Enumerable.Range(0, 150).ToList();
        }

        [Test]
        public void Paginate_FirstPage_ReturnsDefaultPageSize()
        {
            var (items, total, next) = PaginationHelper.Paginate(_items);
            Assert.AreEqual(50, items.Count);
            Assert.AreEqual(150, total);
            Assert.AreEqual("50", next);
        }

        [Test]
        public void Paginate_WithCursor_ReturnsCorrectPage()
        {
            var (items, total, next) = PaginationHelper.Paginate(_items, cursor: "50");
            Assert.AreEqual(50, items.Count);
            Assert.AreEqual(50, items[0]);
            Assert.AreEqual("100", next);
        }

        [Test]
        public void Paginate_LastPage_ReturnsNullCursor()
        {
            var (items, total, next) = PaginationHelper.Paginate(_items, cursor: "100");
            Assert.AreEqual(50, items.Count);
            Assert.IsNull(next);
        }

        [Test]
        public void Paginate_CustomPageSize_Respected()
        {
            var (items, total, next) = PaginationHelper.Paginate(_items, pageSize: 10);
            Assert.AreEqual(10, items.Count);
            Assert.AreEqual("10", next);
        }

        [Test]
        public void Paginate_PageSizeClamped_ToMax200()
        {
            var (items, _, _) = PaginationHelper.Paginate(_items, pageSize: 500);
            Assert.AreEqual(150, items.Count);
        }

        [Test]
        public void Paginate_PageSizeClamped_ToMin1()
        {
            var (items, _, _) = PaginationHelper.Paginate(_items, pageSize: -5);
            Assert.AreEqual(1, items.Count);
        }

        [Test]
        public void Paginate_EmptyList_ReturnsEmpty()
        {
            var (items, total, next) = PaginationHelper.Paginate(new List<int>());
            Assert.AreEqual(0, items.Count);
            Assert.AreEqual(0, total);
            Assert.IsNull(next);
        }

        [Test]
        public void Paginate_InvalidCursor_StartsFromZero()
        {
            var (items, _, _) = PaginationHelper.Paginate(_items, cursor: "invalid");
            Assert.AreEqual(0, items[0]);
        }

        [Test]
        public void ToPaginatedResult_ReturnsSuccessResult()
        {
            var result = PaginationHelper.ToPaginatedResult(_items, pageSize: 10);
            Assert.IsTrue(result.IsSuccess);
        }
    }
}

﻿using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NitroxModel.DataStructures.Util
{
    [TestClass]
    public class OptionalTest
    {
        /// <summary>
        ///     These optional additions should be in <see cref="OptionalHasValueDynamicChecks"/> test method but MSTest
        ///     reuses instances which causes <see cref="Optional{T}.HasValue">Optional{T}.HasValue</see> to be called before the new conditions are added.
        /// </summary>
        [TestInitialize]
        public static void Init()
        {
            Optional.ApplyHasValueCondition<Base>(v => v.GetType() == typeof(A) || v.Threshold > 200); // Cheat: allow check if type A to do more complex tests on Optional<T>.HasValue
            Optional.ApplyHasValueCondition<A>(v => v.Threshold <= 200);
        }
        
        [TestMethod]
        public void OptionalGet()
        {
            Optional<string> op = Optional.Of("test");
            op.Value.Should().Be("test");
        }

        [TestMethod]
        public void OptionalIsPresent()
        {
            Optional<string> op = Optional.Of("test");
            op.HasValue.Should().BeTrue();
        }

        [TestMethod]
        public void OptionalIsNotPresent()
        {
            Optional<string> op = Optional.Empty;
            op.HasValue.Should().BeFalse();
        }

        [TestMethod]
        public void OptionalOrElseValidValue()
        {
            Optional<string> op = Optional.Of("test");
            op.OrElse("test2").Should().Be("test");
        }

        [TestMethod]
        public void OptionalOrElseNoValue()
        {
            Optional<string> op = Optional.Empty;
            op.OrElse("test").Should().Be("test");
        }

        [TestMethod]
        public void OptionalEmpty()
        {
            Optional<string> op = Optional.Empty;
            op.HasValue.Should().BeFalse();
        }

        [TestMethod]
        public void OptionalSetValueNull()
        {
            Optional<Random> op = Optional.Of(new Random());
            Assert.IsTrue(op.HasValue);
            Assert.ThrowsException<ArgumentNullException>(() => { op = null; }, "Setting optional to null should not be allowed.");
            op = Optional.Empty;
            Assert.IsFalse(op.HasValue);
        }

        [TestMethod]
        public void OptionalHasValueDynamicChecks()
        {
            Optional<Base> opBase = Optional.Of(new Base());
            opBase.HasValue.Should().BeTrue();
            opBase.Value.Threshold.Should().Be(202);

            Optional<A> a = Optional.Of(new A());
            a.HasValue.Should().BeTrue();

            Optional<A> actuallyB = Optional.Of<A>(new B());
            actuallyB.HasValue.Should().BeFalse();

            Optional<B> b = Optional.Of(new B());
            b.HasValue.Should().BeFalse();

            // A check should still happen on Base because Optional<Base> includes more-specific-than-itself checks.
            Optional<Base> aAsBase = Optional<Base>.Of((Base)a);
            aAsBase.HasValue.Should().BeTrue();
            aAsBase.Value.Threshold.Should().Be(200);

            // Optional<object> should always do all checks because anything can be in it.
            Optional<object> bAsObj = Optional<object>.Of(new B());
            bAsObj.HasValue.Should().BeFalse();

            // Type C inheritance doesn't allow for type A. But Optional<object> has the check on A. It should skip the A check on C because inheritance doesn't match up.
            Optional<object> cAsObj = Optional<object>.Of(new C());
            cAsObj.HasValue.Should().BeTrue();
            ((C)cAsObj.Value).Threshold.Should().Be(203);
        }

        private class Base
        {
            public virtual int Threshold => 202;
        }

        private class A : Base
        {
            public override int Threshold => 200;
        }

        private class B : A
        {
            public override int Threshold => 201;
        }

        private class C : Base
        {
            public override int Threshold => 203;
        }
    }
}

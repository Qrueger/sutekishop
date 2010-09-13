using System.Web.Mvc;
using Castle.Facilities.NHibernateIntegration;
using NHibernate;
using NUnit.Framework;
using Rhino.Mocks;
using Suteki.Common.Filters;
using Suteki.Common.Tests.TestHelpers;

namespace Suteki.Common.Tests.Filters
{
	[TestFixture]
	public class UnitOfWorkFilterTester
	{
	    ISessionManager sessionManager;
	    ISession session;
	    ITransaction transaction;
		UnitOfWorkFilter filter;

		[SetUp]
		public void Setup()
		{
		    sessionManager = MockRepository.GenerateStub<ISessionManager>();
		    session = MockRepository.GenerateStub<ISession>();
		    transaction = MockRepository.GenerateStub<ITransaction>();

		    sessionManager.Stub(s => s.OpenSession()).Return(session);
		    session.Stub(s => s.BeginTransaction()).Return(transaction);

			filter = new UnitOfWorkFilter(sessionManager);
		}

	    [Test]
	    public void Transaction_should_be_started_when_action_is_run()
	    {
	        filter.OnActionExecuting(new ActionExecutingContext { Controller = new TestController() });
            session.AssertWasCalled(s => s.BeginTransaction());
	    }

		[Test]
		public void Transaction_should_be_commited_when_action_completes()
		{
		    var controller = new TestController();
            filter.OnActionExecuting(new ActionExecutingContext { Controller = controller });
			filter.OnActionExecuted(new ActionExecutedContext { Controller = controller });
			transaction.AssertWasCalled(t => t.Commit());
		}

		[Test]
		public void Transaction_should_be_rolled_back_if_there_are_errors_in_modelstate()
		{
			var controller = new TestController();
			controller.ModelState.AddModelError("foo", "bar");
            filter.OnActionExecuting(new ActionExecutingContext { Controller = controller });
			filter.OnActionExecuted(new ActionExecutedContext { Controller = controller });
			transaction.AssertWasCalled(t => t.Rollback());
		}
	}
}
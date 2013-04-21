using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BitStoreWeb
{
	public class HomeEndpoint
	{
		public HomeViewModel Home(HomeInputModel input)
		{
			return new HomeViewModel();
		}
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BitStoreWeb
{
	public class HomeEndpoint
	{
		public HomeViewModel get(HomeInputModel input)
		{
			return new HomeViewModel();
		}
	}

	public class HomeViewModel
	{

	}

	public class HomeInputModel
	{

	}
}
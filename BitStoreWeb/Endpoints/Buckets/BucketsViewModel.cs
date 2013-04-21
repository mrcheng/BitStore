namespace BitStoreWeb.Endpoints.Buckets
{
	public class BucketsViewModel
	{
		public BucketsInnerViewModel Inner { get; set; }

		public BucketsViewModel()
		{
			Inner = new BucketsInnerViewModel();
		}
	}
}
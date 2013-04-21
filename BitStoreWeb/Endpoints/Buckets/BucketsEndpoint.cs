using BitStoreWeb.Endpoints.Bucket;

namespace BitStoreWeb.Endpoints.Buckets
{
	public class BucketEndpoint
	{
		public BucketsViewModel get_buckets(BucketsInputModel input)
		{
			return new BucketsViewModel();
		}
	}
}
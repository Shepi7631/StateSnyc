namespace StateSync.Client.Network
{
	public class RttTracker
	{
		private const float BaseSnapThreshold = 0.1f;
		private const float BaseSmoothThreshold = 2.0f;
		private const float BaseCorrectionRate = 0.3f;

		public int RttMs { get; private set; }

		public float SnapThreshold => BaseSnapThreshold;

		public float SmoothThreshold(float speed)
		{
			return BaseSmoothThreshold + speed * RttMs / 1000f;
		}

		public float CorrectionRate
		{
			get
			{
				float rate = BaseCorrectionRate - RttMs / 2000f;
				if (rate < 0.1f)
					return 0.1f;
				if (rate > 0.5f)
					return 0.5f;
				return rate;
			}
		}

		public void UpdateRtt(int rttMs)
		{
			RttMs = rttMs;
		}
	}
}

namespace MoneyPenny.Options;

public class RagGroundingCheckOptions
{
    public double RetrievalPassMinScore { get; set; } = 0.55;
    public double RetrievalWarnMinScore { get; set; } = 0.45;
    public double ClaimJaccardPass { get; set; } = 0.18;
    public double ClaimTokenCoveragePass { get; set; } = 0.60;
    public double SymptomCoveragePass { get; set; } = 0.40;
    public int GlobalPassMinScore { get; set; } = 75;
    public int GlobalWarnMinScore { get; set; } = 50;
    public int MinClaimLength { get; set; } = 25;
}

namespace BlindMatchPAS.Services
{
    public class MatchOperationResult
    {
        public MatchOperationStatus Status { get; init; }

        public string Message { get; init; } = string.Empty;

        public int? ProposalId { get; init; }

        public bool Succeeded => Status == MatchOperationStatus.Success;
    }
}

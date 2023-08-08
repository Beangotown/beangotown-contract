using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken;

namespace Contracts.BeangoTownContract
{
    public partial class BeangoTownContractState
    {
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
        internal AEDPoSContractContainer.AEDPoSContractReferenceState ConsensusContract { get; set; }
    }
}
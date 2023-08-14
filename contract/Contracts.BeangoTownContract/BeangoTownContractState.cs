using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Contracts.BeangoTownContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class BeangoTownContractState : ContractState
    {
        // state definitions go here.
        public BoolState Initialized { get; set; }
        
        public SingletonState<Address> Admin { get; set; }
        public SingletonState<GameLimitSettings> GameLimitSettings { get; set; }
        public SingletonState<GridTypeList> GridTypeList { get; set; }
        public MappedState<Address, PlayerInformation> PlayerInformation { get; set; }

        public MappedState<Hash, BoutInformation> BoutInformation { get; set; }
    }
}
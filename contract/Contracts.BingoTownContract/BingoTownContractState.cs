using System.Collections.Generic;
using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace AElf.Contracts.BingoTownContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class BingoTownContractState : ContractState
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
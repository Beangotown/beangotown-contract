using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.Election;
using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Contracts.BingoGameContract;
using Volo.Abp.Threading;

namespace AElf.Contracts.BingoTownContract
{
    public class BingoTownContractTestBase : DAppContractTestBase<BingoTownContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);
        internal BingoTownContractContainer.BingoTownContractStub BingoTownContractStub { get; set; }
        internal BingoTownContractContainer.BingoTownContractStub UserStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
        internal AEDPoSContractImplContainer.AEDPoSContractImplStub AEDPoSContractStub { get; set; }
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        
        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        
        public BingoTownContractTestBase()
        {
            BingoTownContractStub = GetBingoTownContractStub(DefaultKeyPair);
            UserStub = GetBingoTownContractStub(UserKeyPair);
            TokenContractStub = GetTokenContractTester(DefaultKeyPair);
            AEDPoSContractStub = GetAEDPoSContractStub(DefaultKeyPair);
            AsyncHelper.RunSync(() => BingoTownContractStub.Initialize.SendAsync(new Empty()));
            AsyncHelper.RunSync(() => CreateSeedNftCollection(TokenContractStub));
            AsyncHelper.RunSync(() => CreateNftAsync(TokenContractStub,new CreateInput
            {
                Symbol = BingoTownContractConstants.BeanPassSymbol,
                TokenName = "BeanPassSymbol",
                TotalSupply = 10,
                Decimals = 0,
                Issuer = DefaultAddress,
                IsBurnable = true,
                Owner = DefaultAddress
            })
            );
        }

        internal BingoTownContractContainer.BingoTownContractStub GetBingoTownContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<BingoTownContractContainer.BingoTownContractStub>(DAppContractAddress, senderKeyPair);
        }
        
        internal TokenContractContainer.TokenContractStub GetTokenContractTester(ECKeyPair keyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, keyPair);
        }
        
        internal AEDPoSContractImplContainer.AEDPoSContractImplStub GetAEDPoSContractStub(ECKeyPair keyPair)
        {
            return GetTester<AEDPoSContractImplContainer.AEDPoSContractImplStub>(ConsensusContractAddress, keyPair);
        }

        internal async Task CreateSeedNftCollection(TokenContractContainer.TokenContractStub stub)
        {
            var input = new CreateInput
            {
                Symbol = "SEED-0",
                Decimals = 0,
                IsBurnable = true,
                TokenName = "seed Collection",
                TotalSupply = 1,
                Issuer = DefaultAddress
            };
            await stub.Create.SendAsync(input);
        }
        
        internal async Task<CreateInput> CreateNftAsync(TokenContractContainer.TokenContractStub stub,
            CreateInput createInput)
        {
            var input = BuildSeedCreateInput(createInput);
            await stub.Create.SendAsync(input);
            await stub.Issue.SendAsync(new IssueInput
            {
                Symbol = input.Symbol,
                Amount = 1,
                Memo = "ddd",
                To = DefaultAddress
            });
            await stub.Approve.SendAsync(new ApproveInput() { Spender = TokenContractAddress, Symbol = "SEED-1", Amount = 1 });
            await stub.Create.SendAsync(createInput);
            await stub.Issue.SendAsync( new IssueInput
            {
                Symbol = createInput.Symbol,
                Amount = 1,
                Memo = "ddd",
                To = DefaultAddress
            });
            
            return input;
        }
        
        internal CreateInput BuildSeedCreateInput(CreateInput createInput)
        {
            var input = new CreateInput
            {
                Symbol = "SEED-1",
                Decimals = 0,
                IsBurnable = true,
                TokenName = "seed token 1" ,
                TotalSupply = 1,
                Issuer = DefaultAddress,
               ExternalInfo = new ExternalInfo()
              { Value = { 
                      new Dictionary<string, string>()
                  {
                      ["__seed_owned_symbol"] = createInput.Symbol,
                      ["__seed_exp_time"] = "9992145642"
                  }
              }}
           };
           
            return input;
        }
    }
}
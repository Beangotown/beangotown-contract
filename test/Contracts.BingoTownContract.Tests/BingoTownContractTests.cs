using System.Linq;
using System.Threading.Tasks;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Contracts.BingoGameContract;
using Shouldly;
using Xunit;

namespace AElf.Contracts.BingoTownContract
{
    public class BingoTownContractTests : BingoTownContractTestBase
    {
        [Fact]
        public async Task InitializeTests()
        {
            await BingoTownContractStub.Initialize.SendAsync(new Empty());
        }
        
        [Fact]
        public async Task PlayTests()
        {
            var id = await PlayAsync(true);
            var playInformation = await BingoTownContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playInformation.PlayableCount.ShouldBe(BingoTownContractConstants.DailyMaxPlayCount-1);
            playInformation.PlayerAddress.ShouldBe(DefaultAddress);
            var boutInformation = await BingoTownContractStub.GetBoutInformation.CallAsync(new GetBoutInformationInput()
            {
                PlayId = id
            });
            boutInformation.PlayerAddress.ShouldBe(DefaultAddress);
            boutInformation.PlayBlockHeight.ShouldNotBeNull();
            boutInformation.IsComplete.ShouldBe(false); 
            boutInformation.PlayId.ShouldBe(id);
            var newId = await PlayAsync(false);
            var newPlayInformation = await BingoTownContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            newPlayInformation.PlayableCount.ShouldBe(BingoTownContractConstants.DailyMaxPlayCount-2);
            newPlayInformation.PlayerAddress.ShouldBe(DefaultAddress);   
            var newBoutInformation = await BingoTownContractStub.GetBoutInformation.CallAsync(
                new GetBoutInformationInput()
                {
                    PlayId = newId
                });
            newBoutInformation.PlayId.ShouldBe(newId);
            for (int i = 0; i < 3; i++)
            {
                await PlayAsync(true);
            }
            var  s = await BingoTownContractStub.Play.SendWithExceptionAsync(new PlayInput{ResetStart = false});
            s.TransactionResult.Error.ShouldContain("PlayableCount is not enough");
        }
        
        
        
        [Fact]
        public async Task BingoTests()
        {
            int sumScore = 0;
            int sumGridNum = 0;
            for (int i = 0; i < 5; i++)
            {
                var boutInformation = await BingoTest();
                sumScore += boutInformation.Score;
                sumGridNum = (sumGridNum+ boutInformation.GridNum) % 18;
            }

          var playerInfo =  await  BingoTownContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
          playerInfo.SumScore.ShouldBe(sumScore);
          playerInfo.CurGridNum.ShouldBe(sumGridNum);
        }

        private async Task<BoutInformation> BingoTest( )
        {
            var id = await PlayAsync(true);
            for (var i = 0; i < 7; i++)
            {
                await BingoTownContractStub.Bingo.SendWithExceptionAsync(id);
            }

            await BingoTownContractStub.Bingo.SendAsync(id);
            var boutInformation = await BingoTownContractStub.GetBoutInformation.CallAsync(new GetBoutInformationInput
            {
                PlayId = id
            });
            boutInformation.BingoBlockHeight.ShouldNotBeNull();
            boutInformation.GridNum.ShouldBeInRange(1, 6);
            if (boutInformation.GridType == GridType.Blue)
            {
                boutInformation.Score.ShouldBe(1);
            }
            else if (boutInformation.GridType == GridType.Red)
            {
                boutInformation.Score.ShouldBe(5);
            }
            else
            {
                boutInformation.Score.ShouldBeInRange(20, 50);
            }
            boutInformation.IsComplete.ShouldBe(true);
            return boutInformation;
        }

        private async Task<Hash> PlayAsync(bool resetStart)
        {
            var tx = await BingoTownContractStub.Play.SendAsync(new PlayInput
            {
                ResetStart = resetStart
            });
            return tx.TransactionResult.TransactionId;
        }
        [Fact]
        public async Task BingoTests_Fail_AfterRegister()
        {
            var id = await PlayAsync(true);
            var result = await BingoTownContractStub.Bingo.SendWithExceptionAsync(HashHelper.ComputeFrom("test"));
            result.TransactionResult.Error.ShouldContain("Bout not found.");
        }
        
        
        [Fact]
        public async void ChangeAdmin_WithValidInput_ShouldUpdateAdmin()
        {
            var newAdminAddress = new Address { Value = HashHelper.ComputeFrom("NewAdmin").Value };
            await BingoTownContractStub.ChangeAdmin.SendAsync(newAdminAddress);
            var getAdminAddress = await BingoTownContractStub.GetAdmin.CallAsync(new Empty());
            Assert.Equal(newAdminAddress, getAdminAddress);
        }
        [Fact]
        public async void GetAdmin_ShouldReturnAdminAddress(){
            var getAdminAddress = await BingoTownContractStub.GetAdmin.CallAsync(new Empty());
            Assert.Equal(DefaultAddress, getAdminAddress);
        }
        
        [Fact]
        public async Task GetBoutInformationTests_Fail_InvalidInput()
        {
            var result = await BingoTownContractStub.GetBoutInformation.SendWithExceptionAsync(new GetBoutInformationInput());
            result.TransactionResult.Error.ShouldContain("Invalid playId");
            
            result = await BingoTownContractStub.GetBoutInformation.SendWithExceptionAsync(new GetBoutInformationInput
            {
                PlayId = Hash.Empty
            });
            result.TransactionResult.Error.ShouldContain("Bout not found.");
          
        }
        
        [Fact]
        public async Task SetLimitSettingsTests()
        {

            var settings = await BingoTownContractStub.GetGameLimitSettings.CallAsync(new Empty());
            settings.DailyMaxPlayCount.ShouldBe(BingoTownContractConstants.DailyMaxPlayCount);
            settings.DailyPlayCountResetHours.ShouldBe(BingoTownContractConstants.DailyPlayCountResetHours);
            var dailyMaxPlayCount = 4;
            var dailyPlayCountResetHours = 8;
            await BingoTownContractStub.SetGameLimitSettings.SendAsync(new GameLimitSettings()
            {
                DailyMaxPlayCount = dailyMaxPlayCount,
                DailyPlayCountResetHours = dailyPlayCountResetHours
            });

            settings = await BingoTownContractStub.GetGameLimitSettings.CallAsync(new Empty());
            settings.DailyMaxPlayCount.ShouldBe(dailyMaxPlayCount);
            settings.DailyPlayCountResetHours.ShouldBe(dailyPlayCountResetHours);
        }

        [Fact]
        public async Task SetLimitSettingsTests_Fail_NoPermission()
        {

            var result = await UserStub.SetGameLimitSettings.SendWithExceptionAsync(new GameLimitSettings()
            {
                DailyMaxPlayCount = 6,
                DailyPlayCountResetHours = 8
            });

            result.TransactionResult.Error.ShouldContain("No permission");
        }

        [Fact]
        public async Task SetLimitSettingsTests_Fail_InvalidInput()
        {

            var result = await BingoTownContractStub.SetGameLimitSettings.SendWithExceptionAsync(new GameLimitSettings()
            {
                DailyMaxPlayCount = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input");

            result = await BingoTownContractStub.SetGameLimitSettings.SendWithExceptionAsync(new GameLimitSettings
            {
                DailyMaxPlayCount = 1,
                DailyPlayCountResetHours = 80
            });
            result.TransactionResult.Error.ShouldContain("Invalid input");
        }

    }
}
using System;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Contracts.BeangoTownContract
{
    public class BeangoTownContractTests : BeangoTownContractTestBase
    {
        [Fact]
        public async Task InitializeTests()
        {
            await BeangoTownContractStub.Initialize.SendAsync(new Empty());
        }

        [Fact]
        public async Task Play_FailTests()
        {
            var tx = await BeangoTownContractStub.Play.SendWithExceptionAsync(new PlayInput
            {
                ResetStart = true
            });
            tx.TransactionResult.Error.ShouldContain("BeanPass Balance is not enough");
        }


        private async Task<Hash> PlayAsync(bool resetStart)
        {
            var tx = await BeangoTownContractStub.Play.SendAsync(new PlayInput
            {
                ResetStart = resetStart,
                DiceCount = 3

            });
            return tx.TransactionResult.TransactionId;
        }

        [Fact]
        public async void ChangeAdmin_WithValidInput_ShouldUpdateAdmin()
        {
            var newAdminAddress = new Address { Value = HashHelper.ComputeFrom("NewAdmin").Value };
            await BeangoTownContractStub.ChangeAdmin.SendAsync(newAdminAddress);
            var getAdminAddress = await BeangoTownContractStub.GetAdmin.CallAsync(new Empty());
            Assert.Equal(newAdminAddress, getAdminAddress);
        }

        [Fact]
        public async void ChangeAdmin_Fail()
        {
            var newAdminAddress = new Address { Value = HashHelper.ComputeFrom("NewAdmin").Value };
            var checkRe = await UserStub.ChangeAdmin.SendWithExceptionAsync(newAdminAddress);
            checkRe.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async void GetAdmin_ShouldReturnAdminAddress()
        {
            var getAdminAddress = await BeangoTownContractStub.GetAdmin.CallAsync(new Empty());
            Assert.Equal(DefaultAddress, getAdminAddress);
        }

        [Fact]
        public async Task GetBoutInformationTests_Fail_InvalidInput()
        {
            var result =
                await BeangoTownContractStub.GetBoutInformation.SendWithExceptionAsync(new GetBoutInformationInput());
            result.TransactionResult.Error.ShouldContain("Invalid playId");

            result = await BeangoTownContractStub.GetBoutInformation.SendWithExceptionAsync(new GetBoutInformationInput
            {
                PlayId = Hash.Empty
            });
            result.TransactionResult.Error.ShouldContain("Bout not found.");

        }

        [Fact]
        public async Task SetLimitSettingsTests()
        {

            var settings = await BeangoTownContractStub.GetGameLimitSettings.CallAsync(new Empty());
            settings.DailyMaxPlayCount.ShouldBe(BeangoTownContractConstants.DailyMaxPlayCount);
            settings.DailyPlayCountResetHours.ShouldBe(BeangoTownContractConstants.DailyPlayCountResetHours);
            var dailyMaxPlayCount = 4;
            var dailyPlayCountResetHours = 8;
            await BeangoTownContractStub.SetGameLimitSettings.SendAsync(new GameLimitSettings()
            {
                DailyMaxPlayCount = dailyMaxPlayCount,
                DailyPlayCountResetHours = dailyPlayCountResetHours
            });

            settings = await BeangoTownContractStub.GetGameLimitSettings.CallAsync(new Empty());
            settings.DailyMaxPlayCount.ShouldBe(dailyMaxPlayCount);
            settings.DailyPlayCountResetHours.ShouldBe(dailyPlayCountResetHours);
            await PlayInitAsync();
            await PlayAsync(true);
            var playerInformation = await BeangoTownContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playerInformation.PlayableCount.ShouldBe(dailyMaxPlayCount - 1);
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
            var result = await BeangoTownContractStub.SetGameLimitSettings.SendWithExceptionAsync(
                new GameLimitSettings()
                {
                    DailyMaxPlayCount = -1
                });
            result.TransactionResult.Error.ShouldContain("Invalid DailyMaxPlayCount");

            result = await BeangoTownContractStub.SetGameLimitSettings.SendWithExceptionAsync(new GameLimitSettings
            {
                DailyMaxPlayCount = 1,
                DailyPlayCountResetHours = 80
            });
            result.TransactionResult.Error.ShouldContain("Invalid DailyPlayCountResetHours");
        }

        private async Task PlayInitAsync()
        {
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = BeangoTownContractConstants.HalloweenBeanPassSymbol,
                Amount = 1,
                Memo = "ddd",
                To = DefaultAddress
            });
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = BeangoTownContractConstants.BeanSymbol,
                Amount = 100000000000000,
                Memo = "Issue",
                To = DAppContractAddress
            });
        }


        [Fact]
        public async Task CheckBeanPass_Test()
        {
            await PlayInitAsync();
            var BalanceRe = await BeangoTownContractStub.CheckBeanPass.CallAsync(DefaultAddress);
            BalanceRe.Value.ShouldBe(true);
        }

        [Fact]
        public async Task PlayNewTests()
        {
            await PlayInitAsync();
            int sumScore = 0;
            int sumGridNum = 0;
            for (int i = 0; i < 5; i++)
            {
                var boutInformation = await PlayNewTest();
                sumScore += boutInformation.Score;
                sumGridNum = (sumGridNum + boutInformation.GridNum) % 18;
            }

            var playerInfo = await BeangoTownContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playerInfo.TotalBeans.ShouldBe(sumScore);
            playerInfo.CurGridNum.ShouldBe(sumGridNum);
        }

        private async Task<BoutInformation> PlayNewTest()
        {
            var result = await BeangoTownContractStub.Play.SendAsync(new PlayInput()
            {
                DiceCount = 2,
                ResetStart = false
            });
            var boutInformation = await BeangoTownContractStub.GetBoutInformation.CallAsync(new GetBoutInformationInput
            {
                PlayId = result.TransactionResult.TransactionId
            });
            boutInformation.BingoBlockHeight.ShouldNotBeNull();
            boutInformation.GridNum.ShouldBeInRange(1, 18);
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
                var gameRules = await BeangoTownContractStub.GetGameRules.CallAsync(new Empty());
                var minScore = 30;
                var maxScore = 50;
                if (gameRules != null)
                {
                    if (DateTime.UtcNow.ToTimestamp().CompareTo(gameRules.BeginTime) >= 0 &&
                        DateTime.UtcNow.ToTimestamp().CompareTo(gameRules.EndTime) <= 0)
                    {
                        minScore = gameRules.MinScore;
                        maxScore = gameRules.MaxScore;
                    }
                }

                boutInformation.Score.ShouldBeInRange(minScore, maxScore);
            }

            return boutInformation;
        }

        [Fact]
        public async Task SetGameRules_Test()
        {
            var result = await UserStub.SetGameRules.SendWithExceptionAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                MinScore = 1,
                MaxScore = 10
            });

            result.TransactionResult.Error.ShouldContain("No permission");
            result = await BeangoTownContractStub.SetGameRules.SendWithExceptionAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(1).ToTimestamp(),
                MinScore = 1,
                MaxScore = 10
            });
            result.TransactionResult.Error.ShouldContain("Invalid EndTime");
            result = await BeangoTownContractStub.SetGameRules.SendWithExceptionAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                MinScore = 0,
                MaxScore = 10
            });
            result.TransactionResult.Error.ShouldContain("Invalid MinScore");
            result = await BeangoTownContractStub.SetGameRules.SendWithExceptionAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                MinScore = 10,
                MaxScore = 1
            });
            result.TransactionResult.Error.ShouldContain("Invalid MaxScore");
            result = await BeangoTownContractStub.SetGameRules.SendAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                MinScore = 1,
                MaxScore = 10
            });
            result.TransactionResult.TransactionId.ShouldNotBeNull();
            await PlayNewTests();
        }

        [Fact]
        public async Task SetRankingRules_Test()
        {
            var beginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp();
            var result = await UserStub.SetRankingRules.SendWithExceptionAsync(new RankingRules
            {
                BeginTime = beginTime,
                WeeklyTournamentBeginNum = 1,
                RankingHours = 10,
                PublicityHours = 2,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });

            result.TransactionResult.Error.ShouldContain("No permission");
            result = await BeangoTownContractStub.SetRankingRules.SendWithExceptionAsync(new RankingRules
            {
                BeginTime = beginTime,
                WeeklyTournamentBeginNum = 0,
                RankingHours = 10,
                PublicityHours = 2,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });
            result.TransactionResult.Error.ShouldContain("Invalid WeeklyTournamentBeginNum");
            result = await BeangoTownContractStub.SetRankingRules.SendWithExceptionAsync(new RankingRules
            {
                BeginTime = beginTime,
                WeeklyTournamentBeginNum = 1,
                RankingHours = 0,
                PublicityHours = 2,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });
            result.TransactionResult.Error.ShouldContain("Invalid RankingHours");
            result = await BeangoTownContractStub.SetRankingRules.SendAsync(new RankingRules
            {
                BeginTime = beginTime,
                WeeklyTournamentBeginNum = 1,
                RankingHours = 10,
                PublicityHours = 2,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var rankingRules = await UserStub.GetRankingRules.CallAsync(new Empty());
            rankingRules.BeginTime.ShouldBe(beginTime);
            rankingRules.WeeklyTournamentBeginNum.ShouldBe(1);
            rankingRules.RankingHours.ShouldBe(10);
            rankingRules.PublicityHours.ShouldBe(2);
            rankingRules.RankingPlayerCount.ShouldBe(10);
            rankingRules.PublicityPlayerCount.ShouldBe(2);
        }

        [Fact]
        public async Task SetPurchaseChanceConfig_Test()
        {
            var result = await UserStub.SetPurchaseChanceConfig.SendWithExceptionAsync(new PurchaseChanceConfig
            {
                BeansAmount = 10
            });

            result.TransactionResult.Error.ShouldContain("No permission");
            result = await BeangoTownContractStub.SetPurchaseChanceConfig.SendWithExceptionAsync(
                new PurchaseChanceConfig
                {
                    BeansAmount = 0
                });
            result.TransactionResult.Error.ShouldContain("Invalid BeansAmount");
            result = await BeangoTownContractStub.SetPurchaseChanceConfig.SendAsync(new PurchaseChanceConfig
            {
                BeansAmount = 10
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var purchaseChanceConfig = await BeangoTownContractStub.GetPurchaseChanceConfig.CallAsync(new Empty());
            purchaseChanceConfig.BeansAmount.ShouldBe(10);
        }

        [Fact]
        public async Task PlayWithRanking()
        {
            await PlayInitAsync();
            var result = await BeangoTownContractStub.SetRankingRules.SendAsync(new RankingRules
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                WeeklyTournamentBeginNum = 1,
                RankingHours = 10,
                PublicityHours = 0,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            int sumScore = 0;
            int sumGridNum = 0;
            for (int i = 0; i < 5; i++)
            {
                var boutInformation = await PlayNewTest();
                sumScore += boutInformation.Score;
                sumGridNum = (sumGridNum + boutInformation.GridNum) % 18;
            }

            var playerInfo = await BeangoTownContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playerInfo.TotalBeans.ShouldBe(sumScore);
            playerInfo.WeeklyBeans.ShouldBe(sumScore);
            playerInfo.PlayableCount.ShouldBe(BeangoTownContractConstants.DailyMaxPlayCount - 5);
        }

        [Fact]
        public async Task PurchaseChance()
        {
            await PurchaseChanceInit();
            var result = await BeangoTownContractStub.SetPurchaseChanceConfig.SendAsync(new PurchaseChanceConfig
            {
                BeansAmount = 10
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            result = await BeangoTownContractStub.PurchaseChance.SendAsync(new Int32Value
            {
                Value = 10
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var playerInfo = await BeangoTownContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playerInfo.PurchasedChancesCount.ShouldBe(10);
        }

        private async Task PurchaseChanceInit()
        {
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = BeangoTownContractConstants.HalloweenBeanPassSymbol,
                Amount = 1,
                Memo = "ddd",
                To = DefaultAddress
            });
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = BeangoTownContractConstants.BeanSymbol,
                Amount = 100000000000000,
                Memo = "Issue",
                To = DefaultAddress
            });

            await TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = BeangoTownContractConstants.BeanSymbol,
                Amount = 1000000000000,
                Spender = DAppContractAddress,
            });
        }
    }
}   

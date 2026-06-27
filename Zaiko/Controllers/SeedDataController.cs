using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zaiko.Data;
using Zaiko.Models;

namespace Zaiko.Controllers;

[AllowAnonymous]
public class SeedDataController(ApplicationDbContext db, IWebHostEnvironment env) : Controller
{
    public IActionResult Index()
    {
        if (!env.IsDevelopment()) return NotFound();
        return Content("""
            <html><head><meta charset="utf-8">
            <style>
              body { font-family: sans-serif; max-width: 640px; margin: 40px auto; padding: 0 16px; }
              h2 { margin-bottom: 24px; }
              section { border: 1px solid #ddd; border-radius: 8px; padding: 16px 20px; margin-bottom: 16px; }
              section h3 { margin: 0 0 8px; font-size: 1rem; }
              section p { margin: 0 0 12px; color: #555; font-size: 0.9rem; }
              select { margin-right: 8px; padding: 4px 8px; }
              button { padding: 6px 16px; border-radius: 4px; border: 1px solid #999; cursor: pointer; background: #f5f5f5; }
              button.danger { background: #fee; border-color: #c33; color: #c00; }
            </style>
            </head><body>
            <h2>デバッグ: データ管理</h2>
            <section>
              <h3>テストデータ投入</h3>
              <p>業務データ（取引先・商品・色・納品・売上報告）を全て削除してテストデータを投入します。ユーザーはそのまま残ります。</p>
              <form method="post" action="/SeedData/Seed">
                <label>パターン:
                  <select name="pattern">
                    <option value="A">A（最小）: 取引先2・商品4・色3・納品2ヶ月・SR1ヶ月</option>
                    <option value="B">B（標準）: 取引先5・商品10・色5・納品6ヶ月・SR5ヶ月</option>
                    <option value="C">C（大量）: 取引先15・商品30・色8・納品12ヶ月・SR11ヶ月</option>
                  </select>
                </label>
                <button type="submit">投入する</button>
              </form>
            </section>
            <section>
              <h3>業務データを全て削除</h3>
              <p>業務データ（取引先・商品・色・納品・売上報告）を全て削除します。ユーザーはそのまま残るのでログインは引き続き可能です。</p>
              <form method="post" action="/SeedData/DeleteAll" onsubmit="return confirm('業務データを全て削除しますか？');">
                <button type="submit" class="danger">削除する</button>
              </form>
            </section>
            </body></html>
            """, "text/html; charset=utf-8");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAll()
    {
        if (!env.IsDevelopment()) return NotFound();
        db.SalesReports.RemoveRange(db.SalesReports);
        db.Deliveries.RemoveRange(db.Deliveries);
        db.ClientProducts.RemoveRange(db.ClientProducts);
        db.Clients.RemoveRange(db.Clients);
        db.Products.RemoveRange(db.Products);
        db.Colors.RemoveRange(db.Colors);
        await db.SaveChangesAsync();
        return Content("<html><head><meta charset=\"utf-8\"></head><body>業務データを削除しました。ユーザーはそのまま残っています。<a href='/SeedData'>戻る</a></body></html>", "text/html; charset=utf-8");
    }

    [HttpPost]
    public async Task<IActionResult> Seed(string pattern = "A")
    {
        if (!env.IsDevelopment()) return NotFound();

        // 既存データ削除
        db.SalesReports.RemoveRange(db.SalesReports);
        db.Deliveries.RemoveRange(db.Deliveries);
        db.ClientProducts.RemoveRange(db.ClientProducts);
        db.Clients.RemoveRange(db.Clients);
        db.Products.RemoveRange(db.Products);
        db.Colors.RemoveRange(db.Colors);
        await db.SaveChangesAsync();

        int clientCount = pattern switch { "B" => 5, "C" => 15, _ => 2 };
        int productCount = pattern switch { "B" => 10, "C" => 30, _ => 4 };
        int colorCount = pattern switch { "B" => 5, "C" => 8, _ => 3 };
        int delivMonths = pattern switch { "B" => 6, "C" => 12, _ => 2 };
        int srMonths = pattern switch { "B" => 5, "C" => 11, _ => 1 };

        // 色
        var colorNames = new[] { "白", "黒", "アッシュ", "インディゴ", "赤", "青", "緑", "茶" };
        var colors = colorNames.Take(colorCount).Select(n => new Color { ColorName = n }).ToList();
        db.Colors.AddRange(colors);
        await db.SaveChangesAsync();

        // 商品（色バリエーション付き）
        var baseNames = new[] { "山菜図鑑Tシャツ", "うどTシャツ", "山菜図鑑パーカー", "うどパーカー",
            "フキ柄Tシャツ", "ゼンマイTシャツ", "タラの芽Tシャツ", "コシアブラTシャツ",
            "山菜図鑑マグカップ", "山菜図鑑エコバッグ",
            "うどマグカップ", "うどエコバッグ", "フキ柄マグカップ", "ゼンマイポーチ",
            "タラの芽ポーチ", "コシアブラポーチ", "山菜図鑑ステッカー", "うどステッカー",
            "フキ柄ステッカー", "ゼンマイステッカー",
            "山菜図鑑タオル", "うどタオル", "フキ柄タオル", "ゼンマイタオル",
            "タラの芽タオル", "コシアブラタオル", "山菜図鑑缶バッジ", "うど缶バッジ",
            "フキ柄缶バッジ", "山菜詰め合わせセット" };

        var products = new List<Product>();
        var rng = new Random(42);
        int productsAdded = 0;
        foreach (var name in baseNames)
        {
            if (productsAdded >= productCount) break;
            int retailPrice = (rng.Next(8, 50) * 100);
            decimal rate = Math.Round(0.5m + rng.Next(0, 6) * 0.05m, 2);
            bool hasColor = productsAdded < (productCount * 2 / 3);
            if (hasColor)
            {
                int colorsForProduct = Math.Min(colorCount, 2 + rng.Next(0, 2));
                var assignedColors = colors.Take(colorsForProduct).ToList();
                foreach (var color in assignedColors)
                {
                    products.Add(new Product { ProductName = name, RetailPrice = retailPrice, CommissionRate = rate, ColorId = color.ColorId });
                    productsAdded++;
                    if (productsAdded >= productCount) break;
                }
            }
            else
            {
                products.Add(new Product { ProductName = name, RetailPrice = retailPrice, CommissionRate = rate });
                productsAdded++;
            }
        }
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        // 取引先
        var clientNames = new[] { "大厳寺高原キャンプ場", "越後湯沢道の駅", "十日町観光センター",
            "津南温泉センター", "越後川口SA", "魚沼市物産館", "南魚沼道の駅", "塩沢織物館",
            "六日町温泉センター", "湯沢高原ロープウェイ", "苗場スキー場売店", "かぐらスキー場",
            "みつまたサービスセンター", "石打丸山スキー場", "舞子スノーリゾート" };

        var clients = clientNames.Take(clientCount).Select((n, i) => new Client
        {
            ClientName = n,
            FaxNumber = $"025-{i + 1:D3}-{(i * 17 + 1234) % 10000:D4}",
            IsActive = true
        }).ToList();
        db.Clients.AddRange(clients);
        await db.SaveChangesAsync();

        // ClientProducts（各取引先にランダムに商品を割り当て）
        var productGroups = products.GroupBy(p => p.ProductName).ToList();
        var cpList = new List<ClientProduct>();
        foreach (var client in clients)
        {
            int numProducts = Math.Min(productGroups.Count, 3 + rng.Next(0, Math.Min(5, productGroups.Count - 2)));
            var chosen = productGroups.OrderBy(_ => rng.Next()).Take(numProducts).ToList();
            int sortOrder = 1;
            foreach (var grp in chosen)
            {
                foreach (var p in grp)
                {
                    cpList.Add(new ClientProduct
                    {
                        ClientId = client.ClientId,
                        ProductId = p.ProductId,
                        CommissionRate = p.CommissionRate,
                        SortOrder = sortOrder
                    });
                }
                sortOrder++;
            }
        }
        db.ClientProducts.AddRange(cpList);
        await db.SaveChangesAsync();

        // 基準月（今月）
        var today = DateTime.Today;
        var baseMonth = new DateOnly(today.Year, today.Month, 1);

        // 納品データとSalesReport
        var deliveries = new List<Delivery>();
        var salesReports = new List<SalesReport>();

        for (int mi = delivMonths - 1; mi >= 0; mi--)
        {
            var monthStart = baseMonth.AddMonths(-mi);
            string ym = monthStart.ToString("yyyy-MM");

            foreach (var client in clients)
            {
                var cps = cpList.Where(cp => cp.ClientId == client.ClientId).ToList();
                if (!cps.Any()) continue;

                // 月初に繰越 or 通常納品
                bool isFirstMonth = mi == delivMonths - 1;
                int delivCount = 1 + rng.Next(0, Math.Min(3, 4));
                var delivDays = new[] { 5, 12, 19, 26 }.Take(delivCount).ToList();

                foreach (var cp in cps)
                {
                    if (isFirstMonth)
                    {
                        // 初月は繰越なし、通常納品のみ
                    }
                    else
                    {
                        // 繰越は前月のSalesReport保存時に作られるので、ここでは不要
                        // (SalesReportをmi順に処理するため先に通常納品を作る)
                    }

                    foreach (var day in delivDays)
                    {
                        int qty = 2 + rng.Next(0, 5);
                        deliveries.Add(new Delivery
                        {
                            ClientId = client.ClientId,
                            ProductId = cp.ProductId,
                            Quantity = qty,
                            DeliveredAt = monthStart.AddDays(day - 1),
                            IsCarryOver = false
                        });
                    }
                }

                // SalesReport（srMonths分、最古から）
                bool hasSR = mi < srMonths;
                if (hasSR)
                {
                    foreach (var cp in cps)
                    {
                        int closingStock = rng.Next(0, 6);
                        salesReports.Add(new SalesReport
                        {
                            ClientId = client.ClientId,
                            ProductId = cp.ProductId,
                            YearMonth = ym,
                            ClosingStock = closingStock
                        });

                        // 繰越: 翌月の月初に繰越納品
                        if (closingStock > 0)
                        {
                            deliveries.Add(new Delivery
                            {
                                ClientId = client.ClientId,
                                ProductId = cp.ProductId,
                                Quantity = closingStock,
                                DeliveredAt = monthStart.AddMonths(1),
                                IsCarryOver = true
                            });
                        }
                    }
                }
            }
        }

        db.Deliveries.AddRange(deliveries);
        db.SalesReports.AddRange(salesReports);
        await db.SaveChangesAsync();

        return Content($"<html><head><meta charset=\"utf-8\"></head><body>パターン{pattern}のテストデータを投入しました。<a href='/'>ホームへ</a></body></html>", "text/html; charset=utf-8");
    }
}

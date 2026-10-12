using Unayka.Core.Catalog;

namespace Unayka.Core.Data.Seed;

public sealed record SeedCategory(string Slug, string Name, string Description, CategoryKind Kind, string ImageId);

public sealed record SeedProduct(
    string Slug,
    string Name,
    string CategorySlug,
    string ShortDescription,
    string Description,
    long PriceMzn,
    long? CompareAtPriceMzn,
    string? Badge,
    bool IsFeatured,
    int Stock,
    int Sales,
    string[] PhotoIds,
    string[] Facts);

public static class CatalogSeed
{
    private const string HotCoffee = "cafes-quentes";
    private const string IcedCoffee = "cafes-gelados";
    private const string Beans = "graos-e-moidos";
    private const string Skin = "cuidados-de-pele";
    private const string Hair = "cabelo";
    private const string Body = "corpo-e-banho";
    private const string NewBadge = "Novo";
    private const string BestSellerBadge = "Mais vendido";
    private const string NutritionGroup = "Tabela nutricional";
    private const string CompositionGroup = "Composição";
    private const string UsageGroup = "Modo de utilização";

    private static readonly string[] CoffeePhotoIds =
    [
        "1495474472287-4d71bcdd2085", "1509042239860-f550ce710b93", "1447933601403-0c6688de566e",
        "1442512595331-e89e73853f31", "1511920170033-f8396924c348", "1461023058943-07fcbe16d735",
        "1514432324607-a09d9b4aefdd", "1559056199-641a0ac8b55e", "1497935586351-b67a49e012bf",
        "1541167760496-1628856ab772", "1507133750040-4a8f57021571", "1517701604599-bb29b565090c",
        "1461988091159-192b6df7054f", "1498804103079-a6351b050096", "1504630083234-14187a9df0f5",
        "1534778101976-62847782c213", "1572442388796-11668a67e53d", "1556679343-c7306c1976bc",
        "1580933073521-dc49ac0d4e6a", "1485808191679-5f86510681a2", "1511537190424-bbbab87ac5eb",
        "1515442261605-65987783cb6a",
    ];

    private static readonly string[] CosmeticPhotoIds =
    [
        "1556228720-195a672e8a03", "1620916566398-39f1143ab7be", "1596462502278-27bfdc403348",
        "1571781926291-c477ebfd024b", "1522335789203-aabd1fc54bc9", "1612817288484-6f916006741a",
        "1598440947619-2c35fc9aa908", "1608248543803-ba4f8c70ae0b", "1556228578-0d85b1a4d571",
        "1570172619644-dfd03ed5d881", "1526947425960-945c6e72858f", "1631730486784-5456119f69ae",
        "1522337360788-8b13dee7a37e", "1522338242992-e1a54906a8da", "1507652313519-d4e9174996dd",
        "1608571423902-eed4a5ad8108", "1585652757141-8837d676fac8", "1544161515-4ab6ce6db874",
        "1540555700478-4be289fbecef", "1515377905703-c4788e51af15", "1519824145371-296894a0daa9",
        "1600857062241-98e5dba7f214", "1523293182086-7651a899d37f", "1512496015851-a90fb38ba796",
        "1487412947147-5cebf100ffc2",
    ];

    public static readonly IReadOnlyList<SeedCategory> Categories =
    [
        new(HotCoffee, "Cafés quentes", "Espressos e bebidas de leite preparados na hora.", CategoryKind.Coffee, "1509042239860-f550ce710b93"),
        new(IcedCoffee, "Cafés gelados", "Bebidas frescas para os dias de calor.", CategoryKind.Coffee, "1507133750040-4a8f57021571"),
        new(Beans, "Grãos e moídos", "Café para preparar em casa, em grão, moído ou em cápsulas.", CategoryKind.Coffee, "1559056199-641a0ac8b55e"),
        new(Skin, "Cuidados de pele", "Rotina simples para uma pele hidratada e protegida.", CategoryKind.Cosmetic, "1556228720-195a672e8a03"),
        new(Hair, "Cabelo", "Lavagem, nutrição e reparação para todos os tipos de cabelo.", CategoryKind.Cosmetic, "1522337360788-8b13dee7a37e"),
        new(Body, "Corpo e banho", "Texturas ricas e aromas suaves para o momento do banho.", CategoryKind.Cosmetic, "1544161515-4ab6ce6db874"),
    ];

    public static readonly IReadOnlyList<SeedProduct> Products =
    [
        new("espresso-da-casa", "Espresso da Casa", HotCoffee,
            "Intenso e aveludado, com final de chocolate negro.",
            "O nosso espresso de assinatura, extraído com uma mistura de grãos de torra média. Corpo cheio, creme espesso e um final longo de cacau.",
            120, null, null, false, 200, 340, Photos(CoffeePhotoIds, 18, 19, 20),
            Nutrition("1 chávena, 30 ml", 2, "0 g", "0 g", "0 g", "0,1 g", "63 mg")),
        new("cappuccino-cremoso", "Cappuccino Cremoso", HotCoffee,
            "Espresso, leite vaporizado e uma espuma generosa.",
            "Equilíbrio perfeito entre café e leite, coroado com espuma sedosa e uma pitada de cacau. Pode ser pedido com leite sem lactose.",
            180, 210, BestSellerBadge, true, 150, 520, Photos(CoffeePhotoIds, 12, 15, 3),
            Nutrition("1 chávena, 200 ml", 98, "4,5 g", "9 g", "8 g", "5 g", "75 mg")),
        new("latte-de-baunilha", "Latte de Baunilha", HotCoffee,
            "Latte suave com xarope de baunilha natural.",
            "Café de torra média com leite vaporizado e baunilha natural. Doce na medida certa, ideal para acompanhar a tarde.",
            195, null, NewBadge, true, 120, 180, Photos(CoffeePhotoIds, 5, 6, 13),
            Nutrition("1 chávena, 300 ml", 165, "5 g", "22 g", "20 g", "6 g", "75 mg")),
        new("mocha-de-chocolate", "Mocha de Chocolate", HotCoffee,
            "Café, chocolate belga e leite cremoso.",
            "Espresso duplo combinado com chocolate negro derretido e leite vaporizado. Finalizado com natas leves e raspas de cacau.",
            210, null, null, false, 100, 260, Photos(CoffeePhotoIds, 0, 1, 21),
            Nutrition("1 chávena, 300 ml", 230, "9 g", "29 g", "26 g", "7 g", "85 mg")),
        new("cold-brew-classico", "Cold Brew Clássico", IcedCoffee,
            "Infusão a frio durante 18 horas, suave e sem acidez.",
            "Café moído grosso infundido em água fria durante 18 horas. Resulta numa bebida limpa, naturalmente doce e pouco ácida, servida com gelo.",
            190, null, null, true, 90, 300, Photos(CoffeePhotoIds, 16, 10, 11),
            Nutrition("1 copo, 350 ml", 5, "0 g", "1 g", "0 g", "0,3 g", "180 mg")),
        new("latte-gelado-de-caramelo", "Latte Gelado de Caramelo", IcedCoffee,
            "Espresso, leite frio e caramelo salgado.",
            "Duas doses de espresso sobre gelo e leite frio, com calda de caramelo salgado. Refrescante e reconfortante ao mesmo tempo.",
            220, 250, BestSellerBadge, true, 110, 610, Photos(CoffeePhotoIds, 11, 10, 5),
            Nutrition("1 copo, 400 ml", 210, "6 g", "33 g", "30 g", "6 g", "120 mg")),
        new("frappe-de-avela", "Frappé de Avelã", IcedCoffee,
            "Café batido com gelo e creme de avelã.",
            "Espresso batido com gelo, leite e creme de avelã torrada. Textura cremosa, servido com natas e um toque de cacau.",
            240, null, NewBadge, false, 80, 95, Photos(CoffeePhotoIds, 10, 16, 17),
            Nutrition("1 copo, 400 ml", 280, "11 g", "38 g", "34 g", "7 g", "90 mg")),
        new("espresso-tonico", "Espresso Tónico", IcedCoffee,
            "Espresso sobre água tónica, gelo e casca de laranja.",
            "Uma combinação surpreendente e muito refrescante: espresso duplo vertido sobre água tónica e gelo, com uma fatia de laranja.",
            185, null, null, false, 70, 140, Photos(CoffeePhotoIds, 17, 11, 20),
            Nutrition("1 copo, 300 ml", 45, "0 g", "10 g", "9 g", "0,2 g", "126 mg")),
        new("cafe-em-grao-gorongosa", "Café em Grão Gorongosa 250 g", Beans,
            "Grão arábica de altitude, torra média, notas de fruta seca.",
            "Grãos de café arábica cultivados nas encostas da Gorongosa, torrados em pequenos lotes. Notas de fruta seca, cacau e mel.",
            650, null, null, true, 60, 210, Photos(CoffeePhotoIds, 7, 9, 14),
            Nutrition("1 dose, 8 g de café", 1, "0 g", "0 g", "0 g", "0,1 g", "80 mg")),
        new("cafe-moido-torra-media", "Café Moído Torra Média 250 g", Beans,
            "Moído para filtro e cafeteira italiana.",
            "Café moído na medida certa para cafeteira italiana, filtro ou prensa francesa. Aroma intenso e sabor equilibrado.",
            590, 690, null, false, 75, 175, Photos(CoffeePhotoIds, 14, 2, 8),
            Nutrition("1 dose, 8 g de café", 1, "0 g", "0 g", "0 g", "0,1 g", "80 mg")),
        new("blend-espresso-unayka", "Blend Espresso Unayka 500 g", Beans,
            "O blend usado no balcão, em embalagem de meio quilo.",
            "A mesma mistura de grãos que usamos no balcão, agora em embalagem de 500 g com válvula de frescura. Ideal para máquinas de espresso.",
            1100, null, BestSellerBadge, true, 40, 280, Photos(CoffeePhotoIds, 9, 7, 4),
            Nutrition("1 dose, 18 g de café", 2, "0 g", "0 g", "0 g", "0,2 g", "120 mg")),
        new("capsulas-compativeis-10", "Cápsulas Compatíveis, caixa de 10", Beans,
            "Dez cápsulas de espresso intenso, compatíveis com as máquinas de cápsulas mais comuns.",
            "Cápsulas de alumínio recicláveis com café de torra escura. Fáceis de usar, com o sabor intenso da casa.",
            420, null, NewBadge, false, 90, 60, Photos(CoffeePhotoIds, 2, 19, 7),
            Nutrition("1 cápsula, 5 g de café", 1, "0 g", "0 g", "0 g", "0,1 g", "70 mg")),
        new("serum-hidratante-hialuronico", "Sérum Hidratante de Ácido Hialurónico 30 ml", Skin,
            "Hidratação profunda e leve, para todos os tipos de pele.",
            "Sérum de textura fluida com ácido hialurónico de duas moléculas e vitamina B5. Preenche, hidrata e deixa a pele luminosa sem sensação oleosa.",
            1250, 1450, BestSellerBadge, true, 45, 230, Photos(CosmeticPhotoIds, 1, 9, 0),
            Composition("30 ml", "Ácido hialurónico, pantenol, glicerina, extrato de aloé vera", "Aplicar 3 a 4 gotas no rosto limpo, de manhã e à noite, antes do creme.", "Todos os tipos de pele")),
        new("creme-facial-karite", "Creme Facial de Karité 50 ml", Skin,
            "Creme nutritivo com manteiga de karité e vitamina E.",
            "Creme rico e confortável, feito com manteiga de karité e vitamina E. Protege a barreira cutânea e devolve maciez à pele seca.",
            980, null, null, true, 55, 190, Photos(CosmeticPhotoIds, 5, 16, 6),
            Composition("50 ml", "Manteiga de karité, vitamina E, óleo de jojoba, glicerina", "Massajar suavemente no rosto e pescoço, uma ou duas vezes por dia.", "Pele seca e normal")),
        new("gel-de-limpeza-suave", "Gel de Limpeza Suave 150 ml", Skin,
            "Limpa sem ressecar, com extrato de camomila.",
            "Gel de limpeza facial com tensioativos suaves e camomila. Remove a oleosidade e as impurezas sem agredir a pele.",
            720, null, null, false, 70, 120, Photos(CosmeticPhotoIds, 7, 8, 3),
            Composition("150 ml", "Camomila, glicerina, pantenol, extrato de pepino", "Aplicar sobre a pele húmida, massajar e enxaguar com água morna.", "Todos os tipos de pele")),
        new("protetor-solar-facial-fps50", "Protetor Solar Facial FPS 50", Skin,
            "Proteção alta, toque seco e sem resíduo branco.",
            "Protetor solar facial com filtros de largo espectro, textura leve e acabamento invisível. Resistente ao suor.",
            1380, 1600, NewBadge, true, 35, 85, Photos(CosmeticPhotoIds, 2, 15, 4),
            Composition("50 ml", "Filtros UVA e UVB, niacinamida, vitamina E, extrato de aloé vera", "Aplicar generosamente 15 minutos antes da exposição solar e renovar a cada duas horas.", "Todos os tipos de pele")),
        new("champo-nutritivo-coco", "Champô Nutritivo de Coco 300 ml", Hair,
            "Limpeza suave com óleo de coco e proteínas de arroz.",
            "Champô de uso diário que limpa sem ressecar. O óleo de coco e as proteínas de arroz dão brilho e maciez ao cabelo.",
            540, null, null, false, 80, 150, Photos(CosmeticPhotoIds, 11, 12, 13),
            Composition("300 ml", "Óleo de coco, proteínas de arroz, glicerina, pantenol", "Aplicar no cabelo molhado, massajar o couro cabeludo e enxaguar.", "Todos os tipos de cabelo")),
        new("condicionador-karite", "Condicionador de Karité 300 ml", Hair,
            "Desembaraça e hidrata do comprimento às pontas.",
            "Condicionador cremoso com manteiga de karité que facilita o pentear e protege as pontas.",
            560, null, null, false, 75, 130, Photos(CosmeticPhotoIds, 13, 11, 20),
            Composition("300 ml", "Manteiga de karité, óleo de abacate, pantenol", "Aplicar nos comprimentos depois do champô, deixar atuar dois minutos e enxaguar.", "Cabelo seco e ondulado")),
        new("mascara-capilar-reparadora", "Máscara Capilar Reparadora 250 ml", Hair,
            "Tratamento intensivo semanal para cabelo danificado.",
            "Máscara de tratamento com queratina vegetal e óleo de argão. Repara fios quebradiços e devolve brilho em apenas dez minutos.",
            890, 1050, BestSellerBadge, true, 50, 240, Photos(CosmeticPhotoIds, 12, 5, 11),
            Composition("250 ml", "Queratina vegetal, óleo de argão, manteiga de karité, vitamina E", "Aplicar uma vez por semana no cabelo húmido, deixar atuar dez minutos e enxaguar.", "Cabelo danificado ou pintado")),
        new("oleo-de-argao-50ml", "Óleo de Argão 50 ml", Hair,
            "Brilho e controlo do frisado, em poucas gotas.",
            "Óleo de argão puro prensado a frio. Pode ser usado nas pontas, no cabelo seco ou como tratamento antes da lavagem.",
            1190, 1390, null, false, 40, 110, Photos(CosmeticPhotoIds, 9, 1, 11),
            Composition("50 ml", "Óleo de argão 100 por cento", "Aplicar 2 a 3 gotas nas pontas do cabelo seco ou húmido.", "Todos os tipos de cabelo")),
        new("manteiga-corporal-karite", "Manteiga Corporal de Karité 200 ml", Body,
            "Nutrição intensa com cheiro suave a baunilha.",
            "Manteiga corporal de textura rica que se absorve rapidamente. Deixa a pele macia durante todo o dia.",
            760, null, null, true, 65, 200, Photos(CosmeticPhotoIds, 14, 15, 17),
            Composition("200 ml", "Manteiga de karité, óleo de coco, baunilha, vitamina E", "Aplicar sobre a pele limpa e seca, massajando até absorver.", "Pele seca")),
        new("sabonete-artesanal-de-cafe", "Sabonete Artesanal de Café, pack de 3", Body,
            "Esfoliante suave com borra de café e óleo de oliva.",
            "Sabonetes feitos em pequenos lotes com borra de café e óleo de oliva. Esfoliam suavemente e perfumam a pele com notas de café.",
            320, null, NewBadge, false, 120, 90, Photos(CosmeticPhotoIds, 10, 21, 18),
            Composition("3 x 100 g", "Borra de café, óleo de oliva, glicerina vegetal", "Esfregar na pele húmida com movimentos circulares e enxaguar.", "Todos os tipos de pele")),
        new("esfoliante-de-cafe-e-coco", "Esfoliante de Café e Coco 250 g", Body,
            "Esfoliante corporal com café moído e óleo de coco.",
            "Esfoliante corporal que alisa e estimula a circulação. O café moído remove as células mortas e o óleo de coco hidrata.",
            650, null, BestSellerBadge, true, 60, 270, Photos(CosmeticPhotoIds, 21, 17, 10),
            Composition("250 g", "Café moído, óleo de coco, açúcar mascavado, vitamina E", "Massajar sobre a pele húmida uma vez por semana e enxaguar.", "Todos os tipos de pele")),
        new("coffret-banho-e-corpo", "Coffret Presente Banho e Corpo", Body,
            "Manteiga corporal, sabonete e esfoliante numa caixa de oferta.",
            "Uma seleção dos nossos favoritos de banho e corpo, numa caixa de oferta pronta a presentear: manteiga corporal, sabonete artesanal e esfoliante.",
            1800, 2100, null, true, 25, 70, Photos(CosmeticPhotoIds, 22, 19, 21),
            Composition("Conjunto de 3 peças", "Manteiga de karité, borra de café, óleo de coco", "Seguir as indicações de cada produto incluído no conjunto.", "Todos os tipos de pele")),
    ];

    private static string[] Photos(string[] pool, params int[] indexes) =>
        indexes.Select(index => pool[index]).ToArray();

    private static string[] Nutrition(
        string serving, int kcal, string fat, string carbs, string sugars, string protein, string caffeine) =>
    [
        $"{NutritionGroup}|Porção|{serving}",
        $"{NutritionGroup}|Valor energético|{kcal} kcal",
        $"{NutritionGroup}|Gorduras|{fat}",
        $"{NutritionGroup}|Hidratos de carbono|{carbs}",
        $"{NutritionGroup}|Dos quais açúcares|{sugars}",
        $"{NutritionGroup}|Proteínas|{protein}",
        $"{NutritionGroup}|Cafeína|{caffeine}",
    ];

    private static string[] Composition(string volume, string ingredients, string usage, string suitableFor) =>
    [
        $"{CompositionGroup}|Conteúdo|{volume}",
        $"{CompositionGroup}|Ingredientes principais|{ingredients}",
        $"{CompositionGroup}|Indicado para|{suitableFor}",
        $"{UsageGroup}|Como usar|{usage}",
    ];
}

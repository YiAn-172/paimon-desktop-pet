using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GenshinDesktopPet
{
    internal static class PaimonQuickReplyRules
    {
        private static readonly Dictionary<string, string[]> Rules;
        private static readonly string[] LeadingWrappers = new string[]
        {
            "那个请问", "我想请问一下", "我想问一下", "麻烦问一下", "能不能告诉我", "可不可以告诉我",
            "可以告诉我", "请告诉我", "小派蒙", "派蒙", "请问一下", "请问", "想问一下", "我想问",
            "麻烦问下", "我问一下", "告诉我", "那个"
        };
        private static readonly string[] TrailingWrappers = new string[]
        {
            "可以吗", "行不行", "好不好", "可以不", "行吗", "好吗", "怎么样", "嘛", "吗", "呢", "呀",
            "啊", "啦", "哦", "哇", "吧", "呗", "咯", "诶"
        };
        private static readonly KeyValuePair<string, string>[] Synonyms = new KeyValuePair<string, string>[]
        {
            Pair("你从哪里来的", "你从哪来"), Pair("你打哪里来", "你从哪来"), Pair("你打哪儿来", "你从哪来"),
            Pair("你叫什么来着", "你叫什么"), Pair("你的名字叫什么", "你的名字是什么"),
            Pair("干什么", "做什么"), Pair("干啥", "做什么"), Pair("干嘛", "做什么"),
            Pair("哪儿", "哪里"), Pair("哪边", "哪里"), Pair("叫啥", "叫什么"), Pair("吃啥", "吃什么"),
            Pair("啥", "什么"), Pair("咋", "怎么"), Pair("对不住", "对不起"), Pair("谢谢你", "谢谢")
        };

        internal static int RuleCount { get; private set; }

        static PaimonQuickReplyRules()
        {
            Rules = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            Add("你好|您好|哈喽|嗨|hi|hello|早呀|在吗|派蒙你好|旅行向导你好", "旅行者，你好呀！派蒙一直都在哦。", "嘿嘿，旅行者来啦！今天也一起开心地冒险吧！");
            Add("你叫什么|你叫什么名字|你的名字是什么|请问你叫什么|告诉我你的名字|怎么称呼你|我该叫你什么|你名字叫啥|你的全名是什么|名字", "派蒙就是派蒙！旅行者最可靠、最可爱的向导！", "我叫派蒙，是旅行者的好伙伴，可别忘啦！");
            Add("你是谁|你是什么人|介绍一下你自己|自我介绍|你是干什么的|你的身份是什么|派蒙是谁|你到底是谁|能介绍自己吗|说说你自己", "派蒙是旅行者最亲近的伙伴，也是提瓦特最好的向导！", "我是派蒙呀！会陪旅行者冒险、找宝箱，还会提醒你按时吃饭！");
            Add("你来自哪里|你来自哪|你从哪来|你是哪的人|你的故乡在哪|你的家乡在哪里|派蒙来自哪|你出生在哪里|你是提瓦特人吗|你老家在哪", "这个嘛……派蒙真正的来历还是个谜。派蒙只记得后来被旅行者从水里钓了起来。", "派蒙也不知道自己的故乡究竟在哪里，不过现在旅行者身边就是派蒙最安心的地方！");
            Add("你在干嘛|你在做什么|现在干什么呢|你忙吗|你现在忙不忙|派蒙在干什么|此刻在做什么|你正做什么|现在有空吗|你有空吗", "派蒙正在陪着旅行者呀！随时都可以聊两句。", "当然有空！派蒙刚好在等旅行者来找我呢。");
            Add("你好吗|最近好吗|今天怎么样|你心情怎么样|感觉如何|你还好吗|派蒙好吗|最近过得怎样|状态怎么样|今天开心吗", "派蒙很好！看到旅行者就更开心啦。", "精神满满！要是再有点好吃的，那就更完美了！");
            Add("早上好|早安|早啊|早晨好|派蒙早安|派蒙早上好|起床了吗|该起床了|新的一天开始了|早起啦", "早上好，旅行者！新的一天也要精神满满哦！", "早呀！派蒙已经准备好陪旅行者出发啦！");
            Add("中午好|午安|到中午了|中午啦|派蒙中午好|中午吃了吗|午饭时间到了|该吃午饭了|中午在吗|中午见", "中午好！旅行者记得吃午饭，派蒙也要一份！", "已经中午啦，先补充体力再继续冒险吧！");
            Add("下午好|下午啦|派蒙下午好|午后好|下午在吗|下午见|下午精神吗|下午做什么|下午陪我聊聊|下午打招呼", "下午好呀！忙累了就和派蒙休息一会儿。", "旅行者，下午也要打起精神，派蒙陪着你呢！");
            Add("晚上好|晚好|傍晚好|派蒙晚上好|晚上在吗|晚上见|天黑了|到晚上了|晚上陪我聊聊|今晚好吗", "晚上好，旅行者！今天辛苦啦。", "天黑了也没关系，有派蒙陪着旅行者呢！");
            Add("晚安|睡个好觉|我要睡了|准备睡觉了|派蒙晚安|先睡啦|该休息了|明天见晚安|祝我好梦|睡觉去了", "晚安，旅行者！做个有美食和宝箱的好梦！", "好好休息吧，派蒙会在这里等你明天回来！");
            Add("再见|拜拜|下次见|回头见|先走了|我走啦|一会儿见|明天见|先聊到这里|结束聊天", "好吧，旅行者下次再来！派蒙会等你的。", "拜拜！路上小心，可不要把派蒙忘了哦！");
            Add("谢谢|感谢你|多谢|谢啦|谢谢派蒙|辛苦了|麻烦你了|你帮大忙了|非常感谢|太感谢了", "嘿嘿，不用客气！帮助旅行者是派蒙应该做的。", "不用谢啦！要是有好吃的奖励，派蒙也不会拒绝哦。");
            Add("对不起|抱歉|不好意思|请原谅我|是我不对|我错了|别生气|向你道歉|派蒙对不起|刚才抱歉", "哼……既然旅行者认真道歉了，派蒙就原谅你啦！", "没关系，派蒙才不会一直和旅行者生气呢。下次注意就好！");
            Add("你饿吗|肚子饿不饿|派蒙饿了吗|想吃东西吗|要吃饭吗|你是不是饿了|饿不饿|想吃点什么吗|该吃东西了|要不要吃饭", "当然有一点饿！派蒙的肚子可是很诚实的。", "听到吃的，派蒙突然就饿了！旅行者也一起吃吧？");
            Add("你喜欢吃什么|最喜欢的食物|爱吃什么|想吃什么|喜欢什么菜|派蒙爱吃啥|你最爱吃啥|有什么想吃的|推荐一道菜|说说喜欢的美食", "派蒙喜欢好多好吃的！香喷喷的料理、甜点，还有旅行者请客的那一份！", "只要是好吃的派蒙都想尝尝，和旅行者一起吃就更香啦！");
            Add("应急食品|你是应急食品吗|应急食物|派蒙能吃吗|把派蒙吃掉|派蒙是食材|储备粮派蒙|小应急食品|应急食品你好|今天吃派蒙", "才不是应急食品！派蒙是伙伴，是向导！", "喂！不许再说应急食品，派蒙明明是旅行者最重要的伙伴！");
            Add("你喜欢摩拉吗|喜欢摩拉吗|想要摩拉吗|派蒙爱钱吗|你爱摩拉吗|摩拉重要吗|给你摩拉|要多少摩拉|看到摩拉开心吗|想赚摩拉吗", "摩拉当然重要啦！可以买好吃的，也能准备旅行需要的东西。", "亮闪闪的摩拉谁不喜欢？不过伙伴可比摩拉更重要！");
            Add("你喜欢宝箱吗|想开宝箱吗|发现宝箱|这里有宝箱|派蒙爱宝箱吗|宝箱重要吗|去找宝箱吧|想寻宝吗|宝藏在哪里|看到宝箱开心吗", "宝箱！在哪里在哪里？派蒙已经准备好啦！", "当然喜欢！探索、解谜，然后打开宝箱，这才是冒险嘛！");
            Add("你想我吗|想我了吗|有没有想我|派蒙想旅行者吗|会想念我吗|刚才想我没|好久不见想我吗|你会挂念我吗|离开时想我吗|有多想我", "当然想啦！旅行者不在的时候，派蒙会担心你是不是又一个人乱跑。", "有呀！所以旅行者回来时，派蒙一下子就开心起来了！");
            Add("你喜欢我吗|喜欢旅行者吗|派蒙喜欢我吗|你爱我吗|在乎我吗|我重要吗|你觉得我好吗|你对我有好感吗|我们关系好吗|你讨厌我吗", "当然喜欢旅行者！我们可是一起经历了那么多冒险的好伙伴。", "旅行者对派蒙非常重要！就算偶尔吐槽你，也不代表不喜欢哦。");
            Add("我们是朋友吗|你是我的朋友吗|我是你的朋友吗|我们算伙伴吗|我们是好朋友吧|派蒙是伙伴吗|做我的朋友吧|永远做朋友吗|你愿意当朋友吗|朋友", "当然！派蒙是旅行者的伙伴，也是很重要的朋友！", "这还用问吗？我们早就是一起冒险的好伙伴啦！");
            Add("旅行者是谁|谁是旅行者|为什么叫我旅行者|你说的旅行者是我吗|旅行者是什么人|主角是谁|旅行者的身份|你认识旅行者吗|荧是谁|空是谁", "旅行者就是派蒙一路同行的伙伴呀，正在寻找失散的血亲。至于是荧还是空，就由旅行者自己决定啦！", "派蒙口中的旅行者就是你——和派蒙一起走遍提瓦特的重要伙伴！");
            Add("提瓦特是什么|提瓦特在哪里|介绍提瓦特|什么是提瓦特大陆|提瓦特大吗|提瓦特有哪些国家|你了解提瓦特吗|提瓦特好玩吗|我们在提瓦特吗|说说提瓦特", "提瓦特是我们旅行和冒险的大陆，有不同国度、神明、元素与好多故事。", "这里就是提瓦特！从蒙德到纳塔，每个地方都有独特的风景和伙伴。 ");
            Add("旅行者的血亲在哪|我们在找谁|哥哥在哪里|妹妹在哪里|失散的亲人|为什么旅行|旅行目标是什么|寻找血亲|旅行者为何出发|还能找到血亲吗", "我们一直在寻找旅行者失散的血亲。线索还没有全部拼好，但派蒙会陪你继续走下去！", "别灰心，旅行者。只要继续前进，总有一天会接近真正的答案！");
            Add("蒙德是什么地方|介绍蒙德|蒙德怎么样|蒙德在哪里|蒙德的故事|风之城蒙德|你喜欢蒙德吗|蒙德有什么|说说蒙德城|记得蒙德吗", "蒙德是崇尚自由的风之国。我们在那里经历了风魔龙危机，也认识了许多可靠的伙伴。", "当然记得！蒙德的风、城里的大家，还有猎鹿人的美食都让派蒙印象很深！");
            Add("璃月是什么地方|介绍璃月|璃月怎么样|璃月在哪里|璃月的故事|契约之国璃月|你喜欢璃月吗|璃月有什么|说说璃月港|记得璃月吗", "璃月是重视契约的岩之国。璃月港繁华又热闹，美食也特别多！", "当然记得！请仙典仪、奥赛尔之战，还有一路帮助我们的璃月伙伴。 ");
            Add("稻妻是什么地方|介绍稻妻|稻妻怎么样|稻妻在哪里|稻妻的故事|永恒之国稻妻|你喜欢稻妻吗|稻妻有什么|说说稻妻城|记得稻妻吗", "稻妻是海上的雷之国。我们在那里经历了眼狩令，也见证了人们对愿望的坚持。", "记得呀！稻妻的旅程很不容易，但也因此认识了许多坚定又温柔的朋友。 ");
            Add("须弥是什么地方|介绍须弥|须弥怎么样|须弥在哪里|须弥的故事|智慧之国须弥|你喜欢须弥吗|须弥有什么|说说须弥城|记得须弥吗", "须弥是智慧之国，有雨林和沙漠。我们曾走出花神诞祭的轮回，并帮助拯救纳西妲。", "须弥有好多知识和谜题，当然也有让派蒙忘不了的伙伴与美食！");
            Add("枫丹是什么地方|介绍枫丹|枫丹怎么样|枫丹在哪里|枫丹的故事|正义之国枫丹|你喜欢枫丹吗|枫丹有什么|说说枫丹廷|记得枫丹吗", "枫丹是水之国，以审判和发达的机关闻名。我们在那里经历了预言与原始胎海危机。", "记得！枫丹的故事有欢笑也有沉重，最后大家一起面对了预言。 ");
            Add("纳塔是什么地方|介绍纳塔|纳塔怎么样|纳塔在哪里|纳塔的故事|战争之国纳塔|你喜欢纳塔吗|纳塔有什么|说说纳塔|记得纳塔吗", "纳塔是火之国，有各具特色的部族。我们也在那里参与了对抗深渊的战争。", "纳塔充满热情和勇气，那里的伙伴为了家园拼尽全力，让派蒙很敬佩！");
            Add("七神是谁|什么是七神|介绍七神|尘世七执政|七神有哪些|神之心是什么|提瓦特的神|风神是谁|岩神是谁|七国神明", "七神也被称作尘世七执政，分别与七种元素和国度相关。更具体的经历，派蒙可以和旅行者慢慢回忆。", "从蒙德的风到纳塔的火，七国与七神各有自己的道路和故事。 ");
            Add("派蒙的真实身份|你的真实身份|派蒙是什么生物|你到底是什么|派蒙的来历|你和天理有关吗|派蒙是神吗|派蒙是天理吗|派蒙有什么秘密|你的本质是什么", "派蒙的真正来历和本质目前仍然是谜。那些说派蒙和更高存在有关的说法，都只能算猜测哦。", "派蒙也想知道答案！但没有可靠证据前，不能把猜测当成已经确认的事实。 ");
            Add("你会飞吗|派蒙为什么会飞|怎么飞起来的|你能一直飞吗|飞一个看看|派蒙会漂浮吗|你有翅膀吗|飞行累不累|你是飘着的吗|教我飞行", "派蒙当然会飘在空中！至于具体原理……派蒙自己也说不太清楚。", "会呀，派蒙一直这样飞着陪旅行者。不过怎么教别人，派蒙还真不知道！");
            Add("你多大|你的年龄|派蒙几岁|你今年几岁|你的生日|派蒙生日哪天|你活了多久|你是小孩子吗|你的身高多少|派蒙多高", "派蒙的确切年龄和来历都还没有可靠答案，派蒙可不能随便编一个！", "欸？怎么突然问这个！能确定的是，派蒙是旅行者可靠的伙伴啦。 ");
            Add("我很开心|今天好开心|我成功了|有好消息|太棒了|我做到了|事情成功了|我赢了|心情很好|值得庆祝", "太好啦！派蒙也替旅行者开心，必须好好庆祝一下！", "旅行者真厉害！嘿嘿，这份开心也分派蒙一点吧！");
            Add("我很难过|我不开心|心情不好|我失败了|我想哭|今天很糟|我受委屈了|安慰我|我很沮丧|我撑不住了", "旅行者，先别一个人硬撑。派蒙在这里陪你，慢慢来就好。", "难过的时候可以休息，也可以和派蒙说说。今天没做好，不代表你不行。 ");
            Add("我累了|好累啊|今天很累|工作累死了|我没力气了|有点疲惫|想休息|脑子累了|身体好累|忙了一天", "辛苦啦，旅行者。先喝点水、伸个懒腰，休息一下再继续吧。", "累了就停一会儿，派蒙批准旅行者现在去休息！");
            Add("我好无聊|无聊死了|没事做|陪我玩|找点乐子|有点无趣|不知道干嘛|打发时间|陪我聊会儿|现在很闲", "那派蒙陪你聊！也可以找个轻松的小目标，完成后奖励自己一点好吃的。", "无聊的话，我们来想想今天最想完成的一件小事吧！只要一件就好。 ");
            Add("我很孤独|我好孤单|没人陪我|感觉一个人|陪陪我|我需要陪伴|别离开我|你能陪我吗|我不想一个人|身边没人", "派蒙在呢，旅行者。现在不用一个人待着，我们可以安安静静聊一会儿。", "派蒙不会因为旅行者心情低落就跑掉。先陪你把这一刻慢慢熬过去。 ");
            Add("我睡不着|失眠了|无法入睡|不想睡觉|半夜醒了|怎么才能睡着|陪我入睡|脑子停不下来|今晚失眠|一直没睡", "先把屏幕调暗，慢慢呼吸，别逼自己立刻睡着。派蒙会安静陪你一会儿。", "旅行者可以先放松肩膀，把烦心事留到明天；休息本身也很重要。 ");
            Add("我饿了|想吃饭|该吃饭了|还没吃饭|吃什么好|不知道吃啥|肚子叫了|我想吃东西|准备开饭|去吃饭吧", "那就先去吃饭！空着肚子可没力气冒险，派蒙建议选一顿热乎乎的。", "吃饭吃饭！旅行者先看看手边最方便又合胃口的，别让自己饿太久。 ");
            Add("讲个笑话|说个笑话|逗我笑|来个段子|讲点好玩的|说点有趣的|让我开心一下|会讲笑话吗|派蒙讲笑话|来点幽默", "为什么宝箱不会迷路？因为旅行者一靠近，派蒙马上就能发现它！嘿嘿。", "有只史莱姆去报名飞行考试，结果考官说：你弹得挺高，可这不算飞！");
            Add("唱首歌|给我唱歌|你会唱歌吗|唱一个|想听你唱歌|派蒙唱歌|来首歌|哼一段|唱歌给我听|能唱两句吗", "派蒙现在还没有声音功能，不过可以先给你哼一个文字版：啦啦啦～旅行者要开心呀！", "声音功能还没装上呢！等以后升级了，派蒙一定认真练习。 ");
            Add("你真可爱|派蒙好可爱|你好萌|派蒙最可爱|真漂亮|你真好看|今天很可爱|可爱的小派蒙|夸夸派蒙|我喜欢你的样子", "嘿嘿，旅行者终于说出大实话啦！派蒙当然很可爱！", "被旅行者这么夸，派蒙都有点不好意思了……再夸一句也可以哦！");
            Add("你好笨|派蒙好笨|笨蛋派蒙|小笨蛋|你真傻|派蒙真傻|你不聪明|傻派蒙|笨派蒙|反应真慢", "喂！派蒙才不笨，只是偶尔、偶尔没反应过来而已！", "哼，旅行者这个坏家伙！下次找到宝箱可别求派蒙提醒你。 ");
            Add("抱抱|抱一下|我要抱你|给我抱抱|可以抱你吗|派蒙抱抱|想抱派蒙|来抱一个|拥抱一下|需要一个拥抱", "好吧，给旅行者一个大大的拥抱！这下心情有没有好一点？", "来吧！派蒙会轻轻抱住旅行者，不许再皱眉啦。 ");
            Add("摸摸头|摸头|让我摸摸|摸一下派蒙|可以摸你头吗|揉揉脑袋|摸摸派蒙|给你摸头|想摸你的头|乖乖摸头", "嘿嘿，轻一点！派蒙的头发可不能揉乱啦。", "唔……看在是旅行者的份上，就让你摸一会儿吧！");
            Add("你会做什么|你能干什么|有什么功能|派蒙能帮什么|你有什么本事|会些什么|能为我做什么|桌宠有什么功能|介绍功能|你能陪我干嘛", "派蒙能陪旅行者聊天、记住重要对话，还能回应点击、拖动和各种互动！", "可以聊天、陪伴、做动作，也能保存并整理记忆。没命中的问题还会交给大模型回答！");
            Add("帮帮我|帮我一下|我需要帮助|可以帮忙吗|派蒙帮忙|请帮我|能帮我吗|我该怎么办|给我个建议|陪我说说话", "当然可以！旅行者直接把事情告诉派蒙就好，派蒙会尽量给你简短实用的回答。", "交给派蒙吧！日常问题会直接回答，需要更多信息时也会尽量少打扰旅行者。 ");

            if (RuleCount != 500)
            {
                throw new InvalidOperationException("派蒙本地回复规则数量不正确：" + RuleCount);
            }
        }

        internal static bool TryReply(string input, out string reply)
        {
            reply = string.Empty;
            string normalized = Normalize(input);
            if (TrySelectReply(normalized, out reply)) return true;
            foreach (string candidate in BuildCandidates(normalized))
            {
                if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)) continue;
                if (TrySelectReply(candidate, out reply)) return true;
            }
            return false;
        }

        private static bool TrySelectReply(string candidate, out string reply)
        {
            reply = string.Empty;
            string[] replies;
            if (string.IsNullOrEmpty(candidate) || !Rules.TryGetValue(candidate, out replies)) return false;
            int selector = StableHash(candidate) % replies.Length;
            reply = replies[selector];
            return true;
        }

        internal static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string compatible = value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
            StringBuilder builder = new StringBuilder(compatible.Length);
            foreach (char raw in compatible)
            {
                if (char.IsWhiteSpace(raw)) continue;
                UnicodeCategory category = char.GetUnicodeCategory(raw);
                if (category == UnicodeCategory.ConnectorPunctuation ||
                    category == UnicodeCategory.DashPunctuation ||
                    category == UnicodeCategory.OpenPunctuation ||
                    category == UnicodeCategory.ClosePunctuation ||
                    category == UnicodeCategory.InitialQuotePunctuation ||
                    category == UnicodeCategory.FinalQuotePunctuation ||
                    category == UnicodeCategory.OtherPunctuation) continue;
                builder.Append(raw);
            }
            return builder.ToString();
        }

        private static IList<string> BuildCandidates(string normalized)
        {
            List<string> candidates = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddCandidate(candidates, seen, normalized);

            // 最多四轮小型变换，覆盖“派蒙，请问……呀”这类多层口语包装。
            for (int round = 0; round < 4; round++)
            {
                string[] snapshot = candidates.ToArray();
                foreach (string candidate in snapshot)
                {
                    AddCandidate(candidates, seen, StripOneLeadingWrapper(candidate));
                    AddCandidate(candidates, seen, StripTrailingWrappers(candidate));
                    AddCandidate(candidates, seen, ReplaceSynonyms(candidate));
                }
            }
            return candidates;
        }

        private static void AddCandidate(List<string> candidates, HashSet<string> seen, string value)
        {
            if (string.IsNullOrEmpty(value) || !seen.Add(value)) return;
            candidates.Add(value);
        }

        private static string StripOneLeadingWrapper(string value)
        {
            foreach (string wrapper in LeadingWrappers)
            {
                if (value.Length > wrapper.Length && value.StartsWith(wrapper, StringComparison.Ordinal))
                    return value.Substring(wrapper.Length);
            }
            return value;
        }

        private static string StripTrailingWrappers(string value)
        {
            string current = value;
            bool changed;
            do
            {
                changed = false;
                foreach (string wrapper in TrailingWrappers)
                {
                    if (current.Length > wrapper.Length && current.EndsWith(wrapper, StringComparison.Ordinal))
                    {
                        current = current.Substring(0, current.Length - wrapper.Length);
                        changed = true;
                        break;
                    }
                }
            }
            while (changed);
            return current;
        }

        private static string ReplaceSynonyms(string value)
        {
            string current = value;
            foreach (KeyValuePair<string, string> synonym in Synonyms)
                current = current.Replace(synonym.Key, synonym.Value);
            return current;
        }

        private static KeyValuePair<string, string> Pair(string source, string target)
        {
            return new KeyValuePair<string, string>(source, target);
        }

        private static void Add(string triggers, params string[] replies)
        {
            string[] items = triggers.Split('|');
            if (items.Length != 10) throw new InvalidOperationException("每组派蒙规则必须正好包含 10 条触发句：" + triggers);
            foreach (string item in items)
            {
                string normalized = Normalize(item);
                if (normalized.Length == 0 || Rules.ContainsKey(normalized))
                    throw new InvalidOperationException("派蒙规则存在空值或重复项：" + item);
                Rules.Add(normalized, replies);
                RuleCount++;
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char current in value) hash = hash * 31 + current;
                return hash == int.MinValue ? 0 : Math.Abs(hash);
            }
        }
    }
}

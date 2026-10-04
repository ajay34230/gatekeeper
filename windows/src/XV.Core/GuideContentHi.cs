namespace XV.Core;

/// <summary>The same user guide in Hinglish (Hindi in English letters). Same chapters, same order, same screenshots as the English guide.
/// Button and tab names are kept exactly as they appear on the screen.</summary>
public static class GuideContentHi
{
    static GuideSection H(int i, string title, string intro, string[] steps, string[] tips) =>
        new(title, intro, steps, tips, GuideContent.Sections[i].Shots);

    public static readonly GuideSection[] Sections =
    [
        H(0, "1. Main window",
            "Command Center aapke base ka server hai. Yeh encrypted records rakhta hai, gate ke phones se baat karta hai aur gate par jo bhi hota hai woh sab dikhata hai.",
            [
                "Left side: live counts (andar kitne log, base par kitni gaadiyan, location flags, total events), gate stations aur paired phones.",
                "Upar ke buttons: Add Soldier, Add Vehicle, Import / Export, ID Card Studio, Visitors, Leave & Overdue, Local Wi-Fi & Pair Device, Comms, lock aur refresh button.",
                "Tabs: Live Feed, Personnel Registry, Vehicle Fleet, Transit Times, Accounts & Devices, Audit Trail, Export, Settings, Help aur About.",
                "Upar right ka search box usi tab ko filter karta hai jo aap dekh rahe hain.",
            ],
            ["Window lock hone par bhi server chalta rehta hai."]),

        H(1, "2. Pehli baar setup (sirf ek baar)",
            "Gate phones kaam kar sakein, isse pehle server ko base ke posts, har Request Point (RP) ka account aur ek paired phone chahiye.",
            [
                "Settings > Station & General > Open Station & General Settings (Stations & Settings) kholiye.",
                "LOCATIONS mein ID (jaise LOC07) aur naam likhiye, phir Add / Update dabaiye. GATES (jaise G02) ke liye bhi yahi kijiye.",
                "Accounts & Devices tab kholiye aur + Create Operator dabaiye. RP ka naam, ID aur password dijiye.",
                "Pair Terminal dabaiye (ya upar ka button Local Wi-Fi & Pair Device). Ek QR code aayega.",
                "Phone mein XV Gatekeeper app kholiye, pair chuniye aur QR scan kijiye. Phone Paired Terminals mein dikhega.",
                "Apne log aur gaadiyan add kijiye (chapter 4 aur 5) ya file se import kijiye.",
            ],
            ["Local pairing ke liye phone aur PC ka same Wi-Fi par hona zaroori hai. Internet se pairing ke liye Cloud Link use kijiye (chapter 17).",
             "Jo operator phone par khud register karte hain, unhe sign in se pehle Accounts & Devices mein approve karna padta hai."]),

        H(2, "3. Live Feed",
            "Har gate scan yahan turant aata hai, sabse naya sabse upar. Har card batata hai kaun ya kaunsi gaadi, kahan, kab aur kisne record kiya.",
            [
                "Filter chips use kijiye: All Events, Personnel, Vehicles, Entries, Exits, Location Flags.",
                "Destination wali vehicle exit par ek line dikhti hai jaise 'To Location 08 - approx 30 min - ON THE WAY'. Gaadi late ho to line laal ho jati hai: 'NOT REACHED'.",
                "Laal Location Flag ka matlab hai ki person ya gaadi us location par scan hui jahan use hona chahiye tha uske alawa kisi aur jagah.",
                "Feed ko spreadsheet mein save karne ke liye Export CSV dabaiye.",
            ],
            ["Har card Audit Trail mein bhi likha jata hai aur badla nahi ja sakta."]),

        H(3, "4. Personnel Registry aur soldiers",
            "Sabhi soldiers aur staff ki list: rank, number, unit, photo aur card ka status.",
            [
                "+ Add Soldier dabaiye. Rank, naam, army number, company, platoon, section, mobile aur baaki fields bhariye, photo lagaiye aur save kijiye.",
                "Record badalne ke liye registry se person kholiye aur edit kijiye. Person kahan-kahan gaya yeh dekhne ke liye History dabaiye.",
                "Ek saath bahut log add karne ke liye Import / Export dabaiye, soldier file (CSV ya Excel) chuniye aur import kijiye. Window batati hai kitne add ya update hue aur kin rows mein galti thi.",
                "Registry save karne ke liye Import / Export dabaiye aur soldiers export kijiye. Aap all, ek company ya selection chun sakte hain.",
            ],
            ["Suspended person ko har gate par tab tak mana kiya jata hai jab tak status wapas active na ho."]),

        H(4, "5. Vehicle Fleet",
            "Sabhi registered gaadiyan: registration, type, model, company aur status. Ek badge batata hai kitni raaste mein hain aur kitni abhi tak pahunchi nahi.",
            [
                "+ Add Vehicle dabaiye aur registration, military registration, type, model, company aur status bhariye.",
                "Gaadi kholkar uski History dekhiye: har entry aur exit, driver aur co-driver, aur uska route chart.",
                "Import / Export se CSV file se bahut saari gaadiyan import kijiye.",
            ],
            ["Gaadi ki details har vehicle route chart aur export ke upar dikhti hain."]),

        H(5, "6. Transit Times - gaadiyon ko locations ke beech dekhna",
            "Jab gaadi gate se niklti hai to guard bata sakta hai ki woh kahan ja rahi hai aur lagbhag kitne minute lagenge. Server ghadi dekhta rehta hai. Woh kabhi yeh nahi maanta ki gaadi pahunch gayi: sirf gate scan, destination RP ya yeh server hi bata sakta hai.",
            [
                "Phone par guard vehicle exit likhte waqt destination aur lagbhag minute bharta hai. Agar un do locations ka standard time pehle se hai to server wahi use karta hai.",
                "Transit Times tab kholiye. VEHICLES ON THE WAY mein har khula trip uske due time ke saath dikhta hai.",
                "STANDARD TIME BETWEEN LOCATIONS: do locations ka standard time save karne ke liye + Time Between Locations dabaiye.",
                "AVERAGE TIME TAKEN har jodi ke liye sabhi gaadiyon ka average, sabse tez aur sabse dheema time dikhata hai, aur standard se compare karta hai.",
                "Haath se trip likhne ke liye + Add Trip dabaiye (jo gaadi bina gate record ke nikli).",
                "Gaadi late ho to server par NOT REACHED pop-up aata hai aur destination RP ke phone par gaadi aur uske nikalne ke time ka notice jata hai.",
                "Pop-up mein Reached... dabakar lagne wala time likhiye; ya Stopped / moved... dabakar jagah aur lagne wala time likhiye; ya cross se band kijiye.",
                "Band kiya hua pop-up 15 minute baad awaaz ke saath wapas aata hai, jab tak trip resolve na ho.",
                "Har late trip ke card par dikhta hai ki destination RP ko bheje notice ka kya hua: SENT / IN TRANSIT (phone ko abhi nahi mila), RECEIVED (phone ne le liya) ya SEEN (RP ne band ya jawab diya).",
                "Saare trips ka PDF, Excel ya CSV banane ke liye Export Transit Report dabaiye.",
            ],
            ["Destination par gaadi ka scan trip ko REACHED kar deta hai. Kisi aur location par scan ho to trip MOVED ELSEWHERE ho jata hai.",
             "Phone ko sirf gaadi aur uske nikalne ka time bataya jata hai. Usse route ke times ya route chart kabhi nahi bheje jate."]),

        H(6, "7. History aur route chart",
            "Kisi bhi person ya gaadi ki History kholiye: har record aur ek route chart dikhega: kin jagahon par gaye, har jagah kitna ruke, beech ka safar aur har trip mein kya hua.",
            [
                "Person ya gaadi par History dabaiye.",
                "Chart upar se neeche padhiye: LEFT, TRAVEL (REACHED, NOT REACHED, MOVED ELSEWHERE ya STOPPED ke saath), ARRIVED aur kitna time ruke.",
                "Gaadiyon ke liye driver aur co-driver dikhte hain, aur driver badalne par ek line batati hai.",
                "Print ke liye ek page ka PDF ya rang-bhara Excel file chahiye to Export Route... dabaiye.",
                "Chhooti hui entry ya exit haath se jodne ke liye person ki history mein + Add Record dabaiye, reason aur remarks ke saath.",
            ],
            ["Haath se record likhne ke liye administrator password lagta hai (agar set hai) aur woh audit trail mein likha jata hai."]),

        H(7, "8. Accounts & Devices",
            "Woh RP accounts jo phones par sign in kar sakte hain, aur jo phones paired hain.",
            [
                "+ Create Operator naya RP account banata hai.",
                "Disable account ko sign in karne se rokta hai. Reset Password naya password set karta hai. Delete account hata deta hai.",
                "Pair Terminal naya pairing QR code dikhata hai.",
                "PAIRED TERMINALS mein har phone, uski location aur aakhri baar kab report kiya yeh dikhta hai. Kisi phone par Revoke Access dabane se woh ruk jata hai aur use apna data mitane ka order jata hai.",
            ],
            ["Revoke kiya hua phone server se agli baar judte hi apna saved data mita deta hai."]),

        H(8, "9. Audit Trail",
            "Har badlav, sign-in aur gate record ka ek tamper-proof log. Har entry pichli entry se judi hoti hai.",
            [
                "Audit Trail tab kholiye. Nayi entries upar hoti hain; details har row ke neeche dikhti hain.",
                "Verify integrity dabaiye. Server poori chain check karta hai aur batata hai ki kuch badla, hataya ya kaata gaya to nahi.",
                "Log save karne ke liye Export CSV dabaiye.",
            ],
            ["Verify integrity niyamit roop se chalaiye aur server ko kisi ko saunpne se pehle bhi."]),

        H(9, "10. Export tab aur reports",
            "Sabhi exports ek jagah: movement reports, route charts aur transit report. Har ek ka apna card hai.",
            [
                "Export tab kholiye.",
                "Log, company ya gaadiyan, dates aur format (PDF, Excel ya CSV) chuniye, phir us card ka export button dabaiye.",
                "Reports ka heading Settings mein badla ja sakta hai (Export heading template).",
                "Route chart export ka apna alag card hai, baaki exports se alag.",
            ],
            ["Exports ke liye administrator password lagta hai (agar set hai)."]),

        H(10, "11. ID Card Studio aur Card Register",
            "Asli QR code ke saath ID card design aur print kijiye, aur har card ka issue, re-issue, print aur kho jane ka record rakhiye.",
            [
                "ID Card Studio dabaiye. Log chuniye (ek, company ya selection), design badaliye aur print kijiye ya PDF save kijiye.",
                "Theme & Emblems > Card style mein Classic (formal card), Modern two sides (front par details, back par QR) ya Modern one side (photo, details aur QR ek hi taraf) chuniye.",
                "Modern cards ke liye rang, light ya dark card, likhawat, extra details (company, blood group aadi) chuniye aur logo upload kijiye: two-sided card par front aur back logo, one-side card par left aur right logo. Slider se logo ka size badlen.",
                "Card Register kholkar har soldier ka card number, issue date, aakhri print aur kho jane ka itihas dekhiye.",
                "Card kho jaye to Report lost... dabaiye. Purana card har gate par turant mana ho jata hai aur agle card ke liye naya QR code ban jata hai.",
                "Register save karne ke liye Export company-wise... dabaiye.",
            ],
            ["Personnel QR codes kabhi expire nahi hote. Code tabhi badalta hai jab card lost report ho ya re-issue ho."]),

        H(11, "12. Visitors aur temporary passes",
            "Din ya kuch samay ke passes jinka QR sirf 'valid from' aur 'valid to' ke beech kaam karta hai.",
            [
                "Visitors dabaiye, phir + New visitor pass.",
                "Visitor ka poora naam, mobile, organisation, ID proof, aane ka purpose aur kisse milna hai yeh bhariye. Dates set kijiye ya Today until 18:00, Next 24 hours ya 7 days use kijiye.",
                "Sirf pass save karna ho to Create pass (no print) dabaiye, ya save karke printable pass kholne ke liye Create & print pass.",
                "Baad mein kisi bhi visitor par Print pass, pass band karne ke liye End pass now, ya visitor kahan gaya yeh dekhne ke liye History dabaiye.",
                "Spreadsheet ke liye Export visitors report... dabaiye.",
            ],
            ["Exit hamesha allowed hai, isliye koi andar fansta nahi. Pass khatam hone ke baad bhi andar rehne wale visitor ko overstay mark kiya jata hai."]),

        H(12, "13. Leave, TD aur overdue alerts",
            "Jo log expected return date ke saath nikle hain unhe track kiya jata hai. Date nikal jaye to server alert deta hai.",
            [
                "Phone un reasons ke liye expected return date poochta hai jo aap Stations & Settings mein chunte hain (jaise Proceeding on Leave, TD).",
                "Leave & Overdue dabaiye aur dekhiye kaun bahar hai. Chips Everyone out ya Overdue only use kijiye.",
                "Koi overdue ho to PC par pop-up aata hai: jaise 'Havildar Name has not returned from leave', reason aur due date ke saath.",
                "Us person ki koi bhi entry absence apne aap band kar deti hai.",
                "Settings > Leave & Overdue Alerts: chuniye kin Locations ya RPs ko yeh alert unke phone par bhi mile. Sirf unhi phones par jata hai.",
            ],
            ["By default alert sirf PC par dikhta hai."]),

        H(13, "14. Comms Center",
            "Server aur paired phones ke beech messages, alerts, calls aur SOS.",
            [
                "Comms dabaiye aur left se ek terminal chuniye.",
                "Message likhkar Send dabaiye, ya Send ALERT dabaiye jisse phone app background mein ho tab bhi baje.",
                "Broadcast alert to all terminals... har phone ko ek alert bhejta hai.",
                "Aapke bheje har message ke neeche uska asli status dikhta hai: sent, in transit (phone offline hai), received (phone ke paas pahunch gaya) ya seen (kisi ne khola). Phone par bhi apne bheje ka yahi status dikhta hai.",
                "Phone se SOS aate hi PC par bhejne wale aur jagah ke saath dikh jata hai.",
                "Terminal ke saath voice aur video calls conversation mein dikhti hain, missed calls laal rang mein.",
            ],
            ["Messages end-to-end encrypted hote hain."]),

        H(14, "15. Settings",
            "Sab kuch jo server ke vyavhaar ko control karta hai.",
            [
                "QUICK STATUS mein abhi ki data sharing, internet access, auto-lock, outbound block, self-registration, administrator password aur heading template dikhte hain.",
                "Station & General: locations, gates, server ka naam, port, custom fields, movement reasons, data protection, diagnostics aur maintenance.",
                "Cloud Link: phones ko internet se PC tak pahunchne deta hai (chapter 17).",
                "Leave & Overdue Alerts: leave alert phone par aur kise milega.",
                "Koi bhi badlav ke baad Save Settings dabaiye.",
            ],
            ["HTTPS port badalne par phones ko dobara pair karna padega."]),

        H(15, "16. Backup, restore, lock aur administrator password",
            "Apne data ko surakshit rakhiye aur wapas paiye.",
            [
                "Import / Export > ENCRYPTED BACKUP & RESTORE > Create encrypted backup... Ek file aur password chuniye. File mein poora database aur settings encrypted rehte hain.",
                "Restore from backup... wahi password maangta hai aur is PC ka data badal deta hai. Yeh sirf soch samajhkar kijiye.",
                "Settings mein administrator password set kijiye. Phir exports, haath ke records aur doosre sensitive kaam se pehle yeh poocha jata hai.",
                "Screen lock karne ke liye upar ka lock button dabaiye. Gate server chalta rehta hai. Unlock ke liye administrator password daliye.",
                "Auto-lock Settings mein set kiye gaye idle time ke baad screen lock kar deta hai.",
            ],
            ["Backup ka password surakshit rakhiye. Uske bina backup khul nahi sakta."]),

        H(16, "17. Cloud Link (internet par phones)",
            "Base ke Wi-Fi se bahar ke phone ko secure tunnel ke zariye server tak pahunchne deta hai.",
            [
                "Settings > Cloud Link kholiye.",
                "Gate server aur comms server ke secure web addresses (URL) daliye jo aapke tunnel ne diye hain.",
                "Phone ko naye QR code se dobara pair kijiye. Ab phone mobile data par bhi kaam karega.",
                "Sirf local Wi-Fi par wapas aane ke liye internet access wapas off kijiye.",
            ],
            ["Internet par bhi saara traffic encrypted rehta hai (TLS aur AES-256-GCM messages)."]),

        H(17, "18. Phone app (gate par RP ke liye)",
            "Phone par XV Gatekeeper app RP ko entry aur exit record karne ke liye hai.",
            [
                "Accounts & Devices mein bane RP account se sign in kijiye aur location tatha gate chuniye.",
                "Card ka QR scan kijiye. App person dikhata hai aur Entry ya Exit poochta hai. Exit ke liye reason chuniye; leave ya TD ke liye expected return date daliye.",
                "Gaadi ke liye vehicle QR scan kijiye, phir driver aur saare saath baithe log, aur confirm kijiye. Vehicle exit par destination aur lagbhag minute daliye.",
                "Agar destination aapke base ki location hai to wahan ke RP ko batata hai jab gaadi nahi pahunchi. RP phir Reached... dabakar lagne wala time likh sakta hai, ya bata sakta hai ki gaadi ruk gayi ya kisi aur jagah chali gayi.",
                "Network na ho to bhi records phone mein rehte hain aur server milte hi bhej diye jate hain.",
                "SOS button position ke saath emergency alert server ko bhejta hai.",
                "Shift handover bina bheje records agle phone ko Bluetooth ya Wi-Fi hotspot se deta hai.",
                "Bhasha (English ya Hindi) aur dark mode app ki settings mein hain.",
            ],
            ["Phone kho jaye to Accounts & Devices mein use revoke kijiye. Agli baar server se judte hi woh apna data mita dega."]),

        H(18, "19. Kuch kaam na kare to",
            "Jaldi ki jaanch.",
            [
                "Phone connect nahi ho raha: dekhiye PC aur phone same Wi-Fi par hain, PC firewall app ko allow karta hai, aur phone revoke nahi hua.",
                "Phone ka time galat hai ya mana kiya ja raha hai: phone par sahi date aur time set kijiye.",
                "Scan mana ho raha hai: Personnel Registry mein person kholkar dekhiye ki status active hai aur card lost report nahi hua.",
                "Pop-up wapas nahi aata: Transit Times tab kholkar trip ko resolve ya snooze kijiye.",
                "Kuch bhi anjaan ho to Audit Trail mein exact entry aur time dekhiye.",
            ],
            []),
    ];
}

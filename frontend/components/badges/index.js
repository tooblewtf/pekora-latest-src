import React from "react";
import { createUseStyles } from "react-jss";
import getFlag from "../../lib/getFlag";
import AdBanner from "../ad/adBanner";

const useBadgeStyles = createUseStyles({
    adContainer: {
        minHeight: '110px',
    },
    title: {
        color: 'var(--text-color-primary)',
        fontSize: '32px',
        fontWeight: '800',
        padding: '5px 0',
        margin: 0,
    },
    subTitle: {
        fontSize: '24px',
        fontWeight: '700',
        color: 'var(--text-color-primary)',
        padding: '5px 0',
        margin: 0,
    },
    badgeContainer: {
        margin: '0 0 6px',
        padding: '15px',
        position: 'relative',
        backgroundColor: 'var(--white-color)',
    },
    badgeImageContainer: {
        width: '75px',
        height: '75px',
        float: 'left',
    },
    badgeImage: {
        height: '100%',
        width: '100%',
        verticalAlign: 'middle',
        border: 0,
        backgroundImage: 'url(/img/roblox_badges.svg)',
        backgroundRepeat: 'no-repeat',
        backgroundSize: '150px auto',
        display: 'inline-block',
    },
    badgeDescription: {
        marginLeft: '85px',
        minHeight: '75px',
        '& h3': {
            fontSize: '20px',
            fontWeight: 700,
            padding: '5px 0',
            margin: 0,
            lineHeight: '1em',
        },
        '& p': {
            fontSize: '16px',
            fontWeight: 400,
            lineHeight: '1.5em',
            margin: 0,
            padding: 0,
            wordWrap: 'break-word',
            hyphens: 'none',
        }
    },
})

const MembershipBadges = [
    {
        name: 'Welcome To The Club Badge',
        desc: 'This badge is awarded to players who have ever belonged to the illustrious Builders Club. These players are part of a long tradition of Marine greatness.',
        iconOne: 2,
        iconTwo: 0,
    },
    {
        name: 'Builders Club Badge',
        desc: 'Members of the illustrious Builders Club display this badge proudly. The Builders Club is a paid premium service. Members receive several benefits: they earn a daily income of 15 Robux, they can sell their creations to others in the Marine Catalog, they get the ability to browse the web site without external ads, and they receive the exclusive Builders Club construction hat.',
        iconOne: 6,
        iconTwo: 0,
    },
    {
        name: 'Turbo Builders Club Badge',
        desc: 'Members of the exclusive Turbo Builders Club are some of the most dedicated Pekorians. The Turbo Builders Club is a paid premium service. Members receive many of the benefits received in the regular Builders Club, in addition to a few more exclusive upgrades: they earn a daily income of 35 Robux, they can sell their creations to others in the Marine Catalog, they get the ability to browse the web site without external ads, they receive the exclusive Turbo Builders Club red site managers hat, and they receive an exclusive gear item.',
        iconOne: 6, // had to remove 1
        iconTwo: 1, // had to add 1
    },
    {
        name: ' Outrageous Builders Club Badge',
        desc: 'Members of Outrageous Builders Club are VIP Pekorians. They are the cream of the crop. The Outrageous Builders Club is a paid premium service. Members receive 100 groups, 85 Robux per day, and many other benefits.',
        iconOne: 7, // had to remove 1
        iconTwo: 0,
    },
]

const CommunityBadges = [
    {
        name: 'Administrator Badge',
        desc: `This badge identifies an account as belonging to a Marine administrator. Only official Marine administrators will possess this badge. If someone claims to be an admin, but does not have this badge, they are potentially trying to mislead you. If this happens, please report abuse and we will delete the imposter's account.`,
        iconOne: 7,
        iconTwo: 1,
    },
    {
        name: 'Veteran Badge',
        desc: 'This badge recognizes members who have played Marine for one year or more. They are stalwart community members who have stuck with us over countless releases, and have helped shape Marine into the game that it is today. These medalists are the true steel, the core of the Projexian history ... and its future.',
        iconOne: 8,
        iconTwo: 0,
    },
    {
        name: 'Friendship Badge',
        desc: 'This badge is given to players who have embraced the Marine community and have made at least 20 friends. People who have this badge are good people to know and can probably help you out if you are having trouble.',
        iconOne: 0,
        iconTwo: 1,
    },
    {
        name: 'Ambassador Badge',
        desc: 'This badge was awarded during the Ambassador Program, which ran from 2009 to 2012. It has been retired and is no longer attainable.',
        iconOne: 2, // had to add 1
        iconTwo: 1,
    },
    {
        name: 'Inviter Badge',
        desc: 'This badge was awarded during the Inviter Program, which ran from 2009 to 2013. It has been retired and is no longer attainable.',
        iconOne: 0,
        iconTwo: 0,
    },
]

const DeveloperBadges = [
    {
        name: 'Homestead Badge',
        desc: 'The homestead badge is earned by having your personal place visited 100 times. Players who achieve this have demonstrated their ability to build cool things that other Pekorians were interested enough in to check out. Get a jump-start on earning this reward by inviting people to come visit your place.',
        iconOne: 4,
        iconTwo: 1,
    },
    {
        name: 'Bricksmith Badge',
        desc: 'The Bricksmith badge is earned by having a popular personal place. Once your place has been visited 1000 times, you will receive this award. Pekorians with Bricksmith badges are accomplished builders who were able to create a place that people wanted to explore a thousand times. They no doubt know a thing or two about putting bricks together.',
        iconOne: 5,
        iconTwo: 0,
    },
    {
        name: 'Official Model Maker Badge',
        desc: 'This badge is awarded to players whose creations are so awesome, Marine endorsed them. Owners of this badge probably have great scripting and building skills.',
        iconOne: 5,
        iconTwo: 1,
    },
]

const GamerBadges = [
    {
        name: 'Combat Initiation Badge',
        desc: 'This badge was granted when a player scored 10 victories in games that use classic combat scripts. It was retired Summer 2015 and is no longer attainable.',
        iconOne: 3,
        iconTwo: 0,
    },
    {
        name: 'Warrior Badge',
        desc: 'This badge was granted when a player scored 100 or more victories in games that use classic combat scripts. It was retired Summer 2015 and is no longer attainable.',
        iconOne: 3,
        iconTwo: 1,
    },
    {
        name: 'Bloxxer Badge',
        desc: 'This badge was granted when a player scored at least 250 victories, and fewer than 250 wipeouts, in games that use classic combat scripts. It was retired Summer 2015 and is no longer attainable.',
        iconOne: 4,
        iconTwo: 0,
    },
]

const Badges = props => {
    const s = useBadgeStyles();
    const processBadge = (badge) => {
        return <div className={s.badgeContainer}>
            <div className={s.badgeImageContainer}>
                <span className={s.badgeImage} style={{ backgroundPosition: `calc(-75px * ${badge.iconTwo}) ` + `calc(-75px * ${badge.iconOne != 0 ? badge.iconOne - 1 : badge.iconOne})` }} />
            </div>
            <div className={s.badgeDescription}>
                <h3>{badge.name}</h3>
                <p>{badge.desc}</p>
            </div>
        </div>
    }

    return <div className='container'>
        <div className={s.adContainer}>
            <AdBanner />
        </div>
        <div className=''>
            <h1 className={s.title}>Badges</h1>
            <h2 className={s.subTitle}>Membership Badges</h2>
            {MembershipBadges.map(processBadge)}
            <h2 className={s.subTitle}>Community Badges</h2>
            {CommunityBadges.map(processBadge)}
            <h2 className={s.subTitle}>Developer Badges</h2>
            {DeveloperBadges.map(processBadge)}
            <h2 className={s.subTitle}>Gamer Badges</h2>
            {GamerBadges.map(processBadge)}
        </div>
    </div>
}

export default Badges;
import React, { useEffect, useState } from "react";
import { createUseStyles, useTheme } from "react-jss";
//import getFlag from "../../lib/getFlag";
import { getFriends } from "../../services/friends";
import { getGameList, getGameSorts } from "../../services/games";
import { multiGetUniverseIcons } from "../../services/thumbnails";
import AuthenticationStore from "../../stores/authentication";
//import GamesPageStore from "../../stores/gamesPage";
import AdSkyscraper from "../ad/adSkyscraper"
//import Games from "../gamesPage";
//import GameRow from "../gameDetails/components/recommendations/GameRow";
import PlayerHeadshot from "../playerHeadshot";
//import useCardStyles from "../userProfile/styles/card";
import FriendEntry from "./components/friendsEntry";
import MyFeed from "./components/myFeed";
//import BlogNews from "./components/blogNews";
import DashboardStore from "./stores/dashboardStore";
import Link from "../link";
import ActionButton from "../actionButton";
import useButtonStyles from "../../styles/buttonStyles";
import GameRow from "../gameDetails/components/recommendations/GameRow";
import { getTheme, themeType } from "../../services/theme";

const useStyles = createUseStyles({
    containerHeader: {
        fontSize: '16px',
        fontWeight: '500',
        lineHeight: '1.4em',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        paddingBottom: '5px',
        // color: p => p.theme === themeType.obc2016 ? 'var(--white-color)' : '',
        color: 'var(--text-color-primary)',
        '& h3': {
            fontSize: '24px',
            fontWeight: 700,
            float: 'left',
            margin: 0,
            lineHeight: '1.4em',
            '@media (max-width: 767px)': {
                fontSize: '21px',
            }
        }
    },
    container: {
        maxWidth: '1338px!important',
        paddingTop: '12px',
        margin: '0 auto',
        display: 'flex',
        '@media (max-width: 1682px)': {
            paddingLeft: '185.5px',
        },
        '@media (max-width: 1338px)': {
            maxWidth: '1154px',
            marginLeft: '10px',
            paddingLeft: '10.5px',
        },
        '@media (max-width: 1154px)': {
            maxWidth: '970px',
            margin: '0 auto!important',
            padding: '0 5px',
        },
    },
    skyScraperLeft: {
        margin: '0 auto',
        marginRight: '24px',
        width: '160px',
        minHeight: '600px',
        float: 'left',
        '@media (max-width: 1358px)': {
            opacity: 0,
        },
        '@media (max-width: 1324px)': {
            display: 'none'
        },
    },
    skyScraperRight: {
        margin: '0 auto',
        marginLeft: '24px',
        width: '160px',
        minHeight: '600px',
        float: 'right',
        '@media (max-width: 1174px)': {
            display: 'none'
        },
    },
    homeContainer: {
        float: 'left',
        maxWidth: '970px',
        width: '100%',
        '@media (max-width: 1174px)': {
            float: 'none',
            margin: '0 auto',
        }
    },
    homeHeader: {
        marginBottom: '12px',
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'start',
        alignItems: 'center',
    },
    headshotWrapper: {
        marginRight: '24px',
        float: 'left',
        backgroundColor: 'var(--white-color)',
        boxShadow: '0 1px 4px 0 rgba(25,25,25,0.3)',
        border: '0 none',
        width: '150px',
        height: '150px',
        padding: 0,
        verticalAlign: 'bottom',
        borderRadius: '50%',
        overflow: 'hidden',
        '@media (max-width: 991px)': {
            width: '90px',
            height: '90px',
            marginLeft: '10px',
        },
        '& img': {
            width: '100%',
            height: '100%',
            border: '0 none',
            borderRadius: '50%',
            verticalAlign: 'middle',
            overflow: 'clip',
        },
        '&:hover': {
            transition: 'box-shadow 200ms ease',
            boxShadow: '0 1px 6px 0 rgba(25,25,25,0.75)',
        },
    },
    homeHeaderText: {
        float: 'left',
        width: 'calc(100% - 180px - 36px)',
        '& h1': {
        }
    },
    helloMessage: {
        fontSize: '36px',
        fontWeight: 800,
        lineHeight: '1em',
        cursor: 'pointer',
        textDecoration: 'none!important',
        //color: p => p.theme === themeType.obc2016 ? 'var(--white-color)' : 'var(--text-color-primary)',
        color: 'var(--text-color-primary)',
    },
    friendSection: {
        minHeight: '1px',
    },
    friendsList: {
        maxHeight: '120px',
        overflow: 'hidden',
        margin: 0,
        padding: 0,
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        '@media (max-width: 991px)': {
            overflow: 'auto',
        },
    },
    homeGamesContainer: {
        display: 'flex',
        flexDirection: 'column'
    },
    sortContainer: {
        margin: '0 0 6px',
    },
    feedNews: {
        display: 'flex',
        flexDirection: 'row',
        gap: '20px',
        width: '100% - 20px',
        '@media (max-width: 991px)': {
            flexDirection: 'column',
            gap: '0',
        },
    },
    newsContainer: {
        float: 'left',
    },
    myFeedContainer: {},
    blogNewsContainer: {},

    seeAllButton: {
        borderColor: 'var(--primary-color)',
        width: '90px',
        padding: '4px',
        fontSize: '14px',
        fontWeight: 500,
        lineHeight: '100%',
        transition: 'box-shadow 200ms ease-in-out',
        boxShadow: 'none',
        '&:hover': {
            borderColor: '#32B5FF',
            boxShadow: '0 1px 3px rgba(150,150,150,0.74)'
        }
    },

    noGames: {
        padding: 'calc((235px - 1.4em) / 2)!important',
    },

    listItem: {
        maxWidth: '166px',
    },
})

const sorts = [{
    token: 'recent',
    name: 'recently_played',
    displayName: 'Recently Played',
    games: [],
}, {
    token: 'Favorited',
    name: 'favorited',
    displayName: 'My Favorites',
    games: [],
}]

const MyDashboard = props => {
    const s = useStyles({theme: getTheme()});
    const buttonStyles = useButtonStyles();
    const auth = AuthenticationStore.useContainer();
    const { friends, setFriends, friendStatus } = DashboardStore.useContainer();
    const [gameSorts, setGameSorts] = useState(null);
    const [icons, setIcons] = useState({});
    useEffect(() => {
        if (!auth.userId) return;
        getFriends({
            userId: auth.userId,
        }).then(d => {
            if (d.length > 0) {
                setFriends(d);
            }
        });
    }, [auth.userId]);

    useEffect(() => {
        let proms = [];
        let gamesList = [];
        let idsForIcons = [];
        for (const item of
            //gameSorts.sorts
            sorts
        ) {
            gamesList.push(item);
            proms.push(
                getGameList({
                    sortToken: item.token,
                    limit: 6,
                    keyword: '',
                }).then(games => {
                    item.games = games.games;
                    games.games.forEach(v => idsForIcons.push(v.universeId));
                })
            )
        }
        Promise.all(proms).then(() => {
            setGameSorts(gamesList);
            multiGetUniverseIcons({
                universeIds: idsForIcons,
                size: '150x150',
            }).then(icons => {
                let obj = {};
                for (const key of icons) { obj[key.targetId] = key.imageUrl };
                setIcons(obj);
            })
        })
    }, []);

    if (!auth.userId)
        return null;

    return <div className={`container ${s.container}`}>
        <div className={s.skyScraperLeft}>
            <AdSkyscraper context='dashboard-left' />
        </div>
        <div className={s.homeContainer}>
            <div className={`col-12 ${s.homeHeader}`}>
                <Link href={`/users/${auth.userId}/profile`}>
                    <a href={`/users/${auth.userId}/profile`} className={s.headshotWrapper}>
                        <PlayerHeadshot id={auth.userId} name={auth.username} />
                    </a>
                </Link>
                <div className={s.homeHeaderText}>
                    <h1>
                        <Link href={`/users/${auth.userId}/profile`} >
                            <a href={`/users/${auth.userId}/profile`} className={s.helloMessage}>Hello, {auth.username}!</a>
                        </Link>
                    </h1>
                </div>
            </div>
            <div className={`col-12 ${s.friendSection}`}>
                <div className={s.containerHeader}>
                    <h3>Friends ({friends?.length || 0})</h3>
                    <span style={{ float: 'right', marginLeft: 'auto' }}>
                        <Link href={`/users/${auth.userId}/friends`}>
                            <a href={`/users/${auth.userId}/friends`}>
                                <ActionButton buttonStyle={buttonStyles.newContinueButton} className={s.seeAllButton} label='See All' />
                            </a>
                        </Link>
                    </span>
                </div>
                <div className={`section-content`}>
                    <ul className={s.friendsList}>
                        {
                            friends && friends.map(v => {
                                return <FriendEntry key={v.id} {...v} />
                            })
                        }
                    </ul>
                </div>
            </div>
            <div className={`col-12 ${s.homeGamesContainer}`}>
                {
                    sorts.map(sort => {
                        return <div className={`col-xs-12 ${s.sortContainer}`}>
                            <div className={s.containerHeader}>
                                <h3>{sort.displayName}</h3>
                                <span>
                                    <Link href={`/games?sortFilter=${sort.token}`}>
                                        <a href={`/games?sortFilter=${sort.token}`}>
                                            <ActionButton buttonStyle={buttonStyles.newContinueButton} className={s.seeAllButton} label='See All' />
                                        </a>
                                    </Link>
                                </span>
                            </div>

                            {sort.games.length === 0 && <div className={`section-content-off ${s.noGames}`}>No games found.</div>
                                ||
                                <GameRow key={sort.token} listItemClass={s.listItem} games={sort.games} />
                            }
                        </div>
                    })
                }
            </div>
            <div className={`col-12 ${s.feedNews}`}>
                <div className={`col-sm-6 ${s.newsContainer} ${s.myFeedContainer}`}>
                    <div className={s.containerHeader} style={{ padding: 0 }}>
                        <h3 style={{ padding: '5px 0', margin: '0 0 6px' }}>My Feed</h3>
                    </div>
                    <div className={`section-content`}>
                        <MyFeed />
                    </div>
                </div>
                <div className={`col-sm-6 ${s.newsContainer} ${s.blogNewsContainer}`}>
                    <div className={s.containerHeader} style={{ padding: 0 }}>
                        <h3 style={{ padding: '5px 0', margin: '0 0 6px' }}>Blog News</h3>
                    </div>
                    <div className={`section-content`}>
                        <p style={{ fontSize: '18px', fontWeight: 500, margin: 0, padding: 0 }}>The blog will be worked on.</p>
                    </div>
                </div>
            </div>
        </div>
        <div className={s.skyScraperRight}>
            <AdSkyscraper context='dashboard-right' />
        </div>
    </div>
}

export default MyDashboard;
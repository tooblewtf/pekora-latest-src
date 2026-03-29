import React, { useEffect, useState } from "react";
import { createUseStyles } from "react-jss";
import AuthenticationStore from "../../stores/authentication";
import NavigationStore from "../../stores/navigation";
import LinkEntry from "./components/linkEntry";
import PlayerHeadshot from "../playerHeadshot";
import { avPageStyleType, getAvPageStyle, getTheme, themeType } from "../../services/theme";
import {
    getPendingApplicationCount,
    getPendingAssets, getPendingGroupIcons,
    getPendingIcons,
    getPendingReportCount
} from "../../services/admin";

const useNavSideBarStyles = createUseStyles({
    container: {
        position: 'fixed',
        top: 0,
        left: 0,
        zIndex: 999,
    },
    card: {
        width: '175px',
        background: p => p.theme === themeType.obc2016 ? '#393939' : 'var(--white-color)',
        color: 'var(--text-color-primary)',
        height: '100vh',
        paddingLeft: '10px',
        paddingRight: '10px',
        marginTop: '12px',
        boxShadow: '0 0 3px rgba(25, 25, 25, 0.3)',
        paddingTop: '40px',
        '@media(max-width: 991px)': {
            paddingTop: '75px',
        }
    },
    username: {
        fontSize: '16px',
        fontWeight: '500',
        marginBottom: 0,
        color: p => p.theme === themeType.obc2016 ? 'var(--white-color)' : 'var(--text-color-primary)',
        textDecoration: 'none',
		display: 'flex',
		alignItems: 'center'
    },
    divider: {
        borderBottom: '1px solid var(--text-color-secondary)',
        borderColor: p => p.theme === themeType.obc2016 ? 'rgba(255, 255, 255, 0.2)' : 'var(--text-color-secondary)',
        height: '2px',
        width: '100%',
        marginTop: '5px',
        marginBottom: '8px'
    },
    upgradeNowButton: {
        marginTop: '10px',
        background: 'var(--primary-color)',
		background: p => p.theme === themeType.obc2016 ? '#85410D' : 'var(--primary-color)',
        fontSize: '15px',
        fontWeight: 500,
        width: '100%',
        paddingTop: '8px',
        paddingBottom: '8px',
        textAlign: 'center',
        color: 'white',
        borderRadius: '4px',
        '&:hover': {
            background: 'var(--primary-color-hover)',
        },
    },
	Wrapheadshot: {
        marginRight: '6px',
        float: 'left',
        backgroundColor: '#d1d1d1',
        border: '0 none',
        width: '22px',
        height: '22px',
        padding: 0,
        verticalAlign: 'middle',
        borderRadius: '50%',
        overflow: 'hidden',
		flexShrink: 0,
        '& img': {
            width: '100%',
            height: '100%',
            border: '0 none',
            borderRadius: '50%',
            verticalAlign: 'middle',
            overflow: 'clip',
        },
    },
});

const NavSideBar = props => {
    const authStore = AuthenticationStore.useContainer();
    const navStore = NavigationStore.useContainer();
    const mainNavBarRef = props.mainNavBarRef;
    const [dimensions, setDimensions] = useState({
        height: window.innerHeight,
        width: window.innerWidth
    })
    const s = useNavSideBarStyles({ theme: getTheme() });
    const [pendingCount, setPendingCount] = useState(69);
    useEffect(() => {
        window.addEventListener('resize', () => {
            setDimensions({
                height: window.innerHeight,
                width: window.innerWidth
            });
        });
        
        const setPending = async () => {
            if (authStore.isStaff) {
                let pendingAssCount = 0;
                // im pretty sure there's a better way to do this while keeping everything asynchronous but ong i forgot 😭😭
                getPendingAssets().then(d => {
                    pendingAssCount += d.length;
                    setPendingCount(pendingAssCount);
                });
                getPendingIcons().then(d => {
                    pendingAssCount += d.length
                    setPendingCount(pendingAssCount);
                });
                getPendingApplicationCount().then(d => {
                    pendingAssCount += d.count
                    setPendingCount(pendingAssCount);
                });
                getPendingReportCount().then(d => {
                    pendingAssCount += d.count
                    setPendingCount(pendingAssCount);
                });
                getPendingGroupIcons().then(d => {
                    pendingAssCount += d.length
                    setPendingCount(pendingAssCount);
                });
            }
        }
        setPending().then();
    }, []);
    useEffect(() => {
        if (pendingCount === 0) setPendingCount(0);
    }, [pendingCount]);
    const paddingTop = mainNavBarRef.current && mainNavBarRef.current.clientHeight + 'px' || 0;
    
    if (navStore.isSidebarOpen === false && dimensions.width <= 1324) {
        return null;
    }
    
    const isStaff = authStore.isStaff;
    
    return <div className={s.container}>
        <div className={s.card}>
            <a href={'/users/' + authStore.userId + '/profile'} className={s.username}>
			<div className={s.Wrapheadshot}>
			   <PlayerHeadshot id={authStore.userId} name={authStore.username} />
			</div>{authStore.username}</a>
            <div className={s.divider}/>
            <LinkEntry theme={getTheme()} name='Home' url='/home' icon='icon-nav-home'/>
            <LinkEntry theme={getTheme()} name='Profile' url={'/users/' + authStore.userId + '/profile'}
                       icon='icon-nav-profile'/>
            <LinkEntry theme={getTheme()} name='Messages' url='/My/Messages' icon='icon-nav-message'
                       count={authStore.notificationCount.messages}/>
            <LinkEntry theme={getTheme()} name='Friends' url={'/users/' + authStore.userId + '/friends'}
                       icon='icon-nav-friends' count={authStore.notificationCount.friendRequests}/>
            <LinkEntry theme={getTheme()} name='Avatar'
                       url={getAvPageStyle() === avPageStyleType.Legacy ? '/My/Character.aspx' : '/My/Avatar'}
                       icon='icon-nav-charactercustomizer'/>
            <LinkEntry theme={getTheme()} name='Inventory' url={'/users/' + authStore.userId + '/inventory'}
                       icon='icon-nav-inventory'/>
            <LinkEntry theme={getTheme()} name='Trade' url='/My/Trades.aspx' icon='icon-nav-trade'
                       count={authStore.notificationCount.trades}/>
            <LinkEntry theme={getTheme()} name='Groups' url='/My/Groups.aspx' icon='icon-nav-group'/>
            <LinkEntry theme={getTheme()} name='Forums' url='/Forum/Default.aspx' icon='icon-nav-forum'/>
			<LinkEntry theme={getTheme()} name='Blog' url='https://blog.silrev.biz/' icon='icon-nav-blog'/>
            {isStaff ? (
                <LinkEntry theme={getTheme()} name='Panel' url='/admin' icon='icon-edit' count={pendingCount}/>
            ) : null}
            <a href='/BuildersClub/Upgrade.ashx'><p className={s.upgradeNowButton}>Upgrade Now</p></a>
			<div>
			<p>Events</p>
			<img src='/img/logo.png' width='150px'></img>
			</div>
        </div>
    </div>
}

export default NavSideBar;